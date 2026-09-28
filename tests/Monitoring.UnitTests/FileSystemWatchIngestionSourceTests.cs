using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monitoring.Infrastructure;
using Moq;

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
        var watchRoot = CreateTempWatchRoot();
        try
        {
            using var source = CreateSource(watchRoot);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var enumerator = source.WatchAsync(cts.Token).GetAsyncEnumerator(cts.Token);
            var moveNextTask = enumerator.MoveNextAsync().AsTask();

            await Task.Delay(200); // let the FileSystemWatcher attach before the file appears
            var filePath = Path.Combine(watchRoot, "photo.png");
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
        var watchRoot = CreateTempWatchRoot();
        try
        {
            using var source = CreateSource(watchRoot);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var enumerator = source.WatchAsync(cts.Token).GetAsyncEnumerator(cts.Token);
            var moveNextTask = enumerator.MoveNextAsync().AsTask();

            await Task.Delay(200);
            var filePath = Path.Combine(watchRoot, "locked.png");

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
        var watchRoot = CreateTempWatchRoot();
        try
        {
            using var source = CreateSource(watchRoot);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var received = new List<string>();

            var watchTask = Task.Run(async () =>
            {
                await foreach (var path in source.WatchAsync(cts.Token))
                    received.Add(path);
            });

            await Task.Delay(200);
            var filePath = Path.Combine(watchRoot, "single.png");
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

    [Fact]
    public async Task Full_channel_logs_the_dropped_event_instead_of_silently_discarding_it()
    {
        var watchRoot = CreateTempWatchRoot();
        try
        {
            var loggerMock = new Mock<ILogger<FileSystemWatchIngestionSource>>();
            using var source = new FileSystemWatchIngestionSource(
                Options.Create(new WatchFolderOptions
                {
                    Path = watchRoot,
                    StabilityRetryDelayMilliseconds = 20,
                    MaxStabilityRetryAttempts = 25,
                    ChannelCapacity = 1,
                }),
                loggerMock.Object,
                new ActivitySource("Test.Monitoring.Ingestion"));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var enumerator = source.WatchAsync(cts.Token).GetAsyncEnumerator(cts.Token);
            var moveNextTask = enumerator.MoveNextAsync().AsTask();

            await Task.Delay(200); // let the FileSystemWatcher attach before the burst

            // A same-instant burst against a single-slot channel: the stability check on each
            // file (opening it exclusively) is slower than the writes below, so the channel
            // fills up and TryWrite must reject the overflow instead of silently evicting it.
            for (var i = 0; i < 30; i++)
            {
                await File.WriteAllBytesAsync(Path.Combine(watchRoot, $"burst-{i}.png"), [1, 2, 3]);
            }

            Assert.True(await moveNextTask); // the channel still delivers at least the first file

            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }
        finally
        {
            Directory.Delete(watchRoot, recursive: true);
        }
    }
}
