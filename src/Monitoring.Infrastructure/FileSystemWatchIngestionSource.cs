using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monitoring.Application;

namespace Monitoring.Infrastructure;

/// <summary>
/// <see cref="IIngestionSource"/> adapter: a <see cref="FileSystemWatcher"/> raises <c>Created</c>
/// events into a bounded <see cref="Channel{T}"/>; <see cref="WatchAsync"/> drains that channel and
/// retries a stability check (open exclusively with <see cref="FileShare.None"/>) with backoff
/// before yielding the path, per the <c>motion-image-ingestion</c> spec's "Debounce And Stability
/// Check" requirement.
///
/// <para>
/// Reuses the exact <c>ai-access-control</c>/research.md "FileSystemWatcher reliability" findings:
/// the watcher's internal buffer can overflow under a burst of events (Microsoft's own docs warn
/// "the buffer can overflow ... it will only provide blanket notification"), surfaced via the
/// <c>Error</c> event — subscribed to here for logging, since the docs describe it as the supported
/// way to detect (not prevent) loss, not a documented polling-fallback recommendation. Partial
/// writes are handled by the exclusive-open stability retry, never assumed away.
/// </para>
/// </summary>
public sealed class FileSystemWatchIngestionSource : IIngestionSource, IDisposable
{
    private readonly WatchFolderOptions _options;
    private readonly ILogger<FileSystemWatchIngestionSource> _logger;
    private readonly ActivitySource _activitySource;
    private readonly Channel<string> _channel;
    private FileSystemWatcher? _watcher;

    public FileSystemWatchIngestionSource(
        IOptions<WatchFolderOptions> options,
        ILogger<FileSystemWatchIngestionSource> logger,
        [FromKeyedServices(MonitoringObservability.ActivitySourceName)] ActivitySource activitySource)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));

        _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(Math.Max(1, _options.ChannelCapacity))
        {
            SingleReader = true,
            SingleWriter = false,
            // Wait (not DropOldest): under DropOldest, TryWrite below always succeeds by silently
            // evicting the oldest queued path, so a burst of creates could lose files with no log
            // line ever firing. Wait makes TryWrite correctly return false when full, so the drop
            // below is actually observed instead of happening invisibly.
            FullMode = BoundedChannelFullMode.Wait,
        });
    }

    public async IAsyncEnumerable<string> WatchAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_options.Path);

        _watcher = new FileSystemWatcher(_options.Path)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        _watcher.Created += OnCreated;
        _watcher.Error += OnError;
        _watcher.EnableRaisingEvents = true;

        try
        {
            await foreach (var candidatePath in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (await IsStableAsync(candidatePath, cancellationToken).ConfigureAwait(false))
                {
                    yield return candidatePath;
                }
            }
        }
        finally
        {
            _watcher.Created -= OnCreated;
            _watcher.Error -= OnError;
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        if (Directory.Exists(e.FullPath))
        {
            return; // directories aren't ingestible files
        }

        if (!_channel.Writer.TryWrite(e.FullPath))
        {
            _logger.LogWarning("Ingestion channel is full; dropped event for {Path}.", e.FullPath);
        }
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        // Per research.md: an Error event fires "when the internal buffer overflows" — the
        // supported way to detect (not prevent) lost change notifications.
        _logger.LogError(e.GetException(), "FileSystemWatcher reported an error (possible internal buffer overflow).");
    }

    private async Task<bool> IsStableAsync(string path, CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("Monitoring.Ingest");
        activity?.SetTag("file.path", path);

        for (var attempt = 1; attempt <= _options.MaxStabilityRetryAttempts; attempt++)
        {
            try
            {
                using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
                activity?.SetTag("debounce.attempts", attempt);
                return true;
            }
            catch (FileNotFoundException)
            {
                // File disappeared before it could be validated — nothing left to ingest.
                activity?.SetTag("debounce.attempts", attempt);
                return false;
            }
            catch (IOException)
            {
                // Still being written or locked by another process — retry with backoff instead
                // of failing immediately, per the "Debounce And Stability Check" requirement.
                await Task.Delay(TimeSpan.FromMilliseconds(_options.StabilityRetryDelayMilliseconds * attempt), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        activity?.SetTag("debounce.attempts", _options.MaxStabilityRetryAttempts);
        _logger.LogWarning("File {Path} did not stabilize after {Attempts} attempts; skipping.", path, _options.MaxStabilityRetryAttempts);
        return false;
    }

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        _watcher?.Dispose();
    }
}
