using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monitoring.Infrastructure;

namespace Monitoring.UnitTests;

/// <summary>
/// Infrastructure tests for <see cref="FileSystemWatchIngestionSource"/>, per the
/// <c>motion-image-ingestion</c> spec's "Folder-Watch Detection", "Debounce And Stability Check",
/// and "One Independent Workflow Run Per Validated File" requirements.
/// </summary>
public sealed class FileSystemWatchIngestionSourceTests
{
    private static string CreateTempWatchRoot() => Directory.CreateTempSubdirectory("camguard-watch-").FullName;

    private static FileSystemWatchIngestionSource CreateSource(string watchRoot, int retryDelayMs = 20, int maxRetries = 25) =>
        new(
            Options.Create(new WatchFolderOptions
            {
                Path = watchRoot,
                StabilityRetryDelayMilliseconds = retryDelayMs,
                MaxStabilityRetryAttempts = maxRetries,
            }),
            NullLogger<FileSystemWatchIngestionSource>.Instance,
            new ActivitySource("Test.Monitoring.Ingestion"));

    [Fact]
    public async Task New_file_triggers_ingestion()
    {
        string watchRoot = CreateTempWatchRoot();
        try
        {
            using FileSystemWatchIngestionSource source = CreateSource(watchRoot);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using IAsyncEnumerator<string> enumerator = source.WatchAsync(cts.Token).GetAsyncEnumerator(cts.Token);
            Task<bool> moveNextTask = enumerator.MoveNextAsync().AsTask();

            await Task.Delay(200); // let the FileSystemWatcher attach before the file appears
            string filePath = Path.Combine(watchRoot, "photo.png");
            await File.WriteAllBytesAsync(filePath, [1, 2, 3]);

            Assert.True(await moveNextTask);
            Assert.Equal(filePath, enumerator.Current);
        }
        finally
        {
            Directory.Delete(watchRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Locked_file_is_retried_with_backoff_until_stable()
    {
        string watchRoot = CreateTempWatchRoot();
        try
        {
            using FileSystemWatchIngestionSource source = CreateSource(watchRoot);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using IAsyncEnumerator<string> enumerator = source.WatchAsync(cts.Token).GetAsyncEnumerator(cts.Token);
            Task<bool> moveNextTask = enumerator.MoveNextAsync().AsTask();

            await Task.Delay(200);
            string filePath = Path.Combine(watchRoot, "locked.png");

            using (var lockedStream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                await lockedStream.WriteAsync(new byte[] { 1, 2, 3 });
                await lockedStream.FlushAsync();
                await Task.Delay(400); // hold the exclusive lock while the source retries the stability check
            } // lock released here — the next stability retry must now succeed instead of failing permanently

            Assert.True(await moveNextTask);
            Assert.Equal(filePath, enumerator.Current);
        }
        finally
        {
            Directory.Delete(watchRoot, recursive: true);
        }
    }

    [Fact]
    public async Task One_validated_file_yields_exactly_one_ingestion_event()
    {
        string watchRoot = CreateTempWatchRoot();
        try
        {
            using FileSystemWatchIngestionSource source = CreateSource(watchRoot);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var received = new List<string>();

            Task watchTask = Task.Run(async () =>
            {
                await foreach (string path in source.WatchAsync(cts.Token))
                    received.Add(path);
            });

            await Task.Delay(200);
            string filePath = Path.Combine(watchRoot, "single.png");
            await File.WriteAllBytesAsync(filePath, [1, 2, 3]);

            await Task.Delay(1000); // allow the stability check (and any duplicate watcher events) to settle
            await cts.CancelAsync();
            try
            {
                await watchTask;
            }
            catch (OperationCanceledException)
            {
                // Expected — cancellation is how this test stops the otherwise-infinite watch loop.
            }

            Assert.Single(received);
            Assert.Equal(filePath, received[0]);
        }
        finally
        {
            Directory.Delete(watchRoot, recursive: true);
        }
    }
}
