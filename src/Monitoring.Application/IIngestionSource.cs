namespace Monitoring.Application;

/// <summary>
/// Port for the folder-watch ingestion stream, per the <c>motion-image-ingestion</c> spec. The
/// only implementation is <c>FileSystemWatchIngestionSource</c> (Infrastructure), backed by
/// <c>FileSystemWatcher</c> + a bounded debounce-retry channel.
/// </summary>
public interface IIngestionSource
{
    /// <summary>Yields validated, debounced/stability-checked file paths ready for one independent workflow run each.</summary>
    IAsyncEnumerable<string> WatchAsync(CancellationToken cancellationToken);
}
