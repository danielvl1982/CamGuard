namespace Monitoring.Infrastructure;

/// <summary>
/// Configuration for the folder-watch drop location, per the <c>motion-image-ingestion</c> spec.
/// Consumed by both <see cref="FileSystemWatchIngestionSource"/> (watches <see cref="Path"/>) and
/// <see cref="FileSystemImageArchiver"/> (creates/uses <c>processed/</c>/<c>quarantine/</c>
/// subfolders directly under <see cref="Path"/>).
/// </summary>
public sealed class WatchFolderOptions
{
    /// <summary>Configuration section name this options type binds from (e.g. <c>appsettings.json</c>).</summary>
    public const string SectionName = "WatchFolder";

    /// <summary>Root drop folder watched for newly created image files.</summary>
    public string Path { get; set; } = "watch";

    /// <summary>Base delay (milliseconds) between stability-check retries when a detected file is still locked/being written; scaled by attempt number.</summary>
    public int StabilityRetryDelayMilliseconds { get; set; } = 200;

    /// <summary>Maximum number of stability-check retries before a detected file is given up on and skipped.</summary>
    public int MaxStabilityRetryAttempts { get; set; } = 5;

    /// <summary>Bounded ingestion channel capacity — bounds memory if <see cref="System.IO.FileSystemWatcher"/> events arrive faster than files can be validated.</summary>
    public int ChannelCapacity { get; set; } = 256;
}
