namespace Monitoring.Application;

/// <summary>
/// Port for archiving an ingested file once its workflow run completes, per the
/// <c>motion-image-ingestion</c> spec's "Completion Archival" requirement. The only
/// implementation is <c>FileSystemImageArchiver</c> (Infrastructure).
/// </summary>
public interface IImageArchiver
{
    Task<string> MoveToProcessedAsync(string sourcePath, CancellationToken cancellationToken);

    Task<string> MoveToQuarantineAsync(string sourcePath, CancellationToken cancellationToken);
}
