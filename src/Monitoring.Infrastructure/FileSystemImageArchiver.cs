using Microsoft.Extensions.Options;
using Monitoring.Application;

namespace Monitoring.Infrastructure;

/// <summary>
/// <see cref="IImageArchiver"/> adapter moving a completed file to a <c>processed/</c> or
/// <c>quarantine/</c> subfolder directly under <see cref="WatchFolderOptions.Path"/>, per the
/// <c>motion-image-ingestion</c> spec's "Completion Archival" requirement. Both subfolders are
/// created on first use if missing.
/// </summary>
public sealed class FileSystemImageArchiver : IImageArchiver
{
    private const string ProcessedFolderName = "processed";
    private const string QuarantineFolderName = "quarantine";

    private readonly WatchFolderOptions _options;

    public FileSystemImageArchiver(IOptions<WatchFolderOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public Task<string> MoveToProcessedAsync(string sourcePath, CancellationToken cancellationToken) =>
        MoveAsync(sourcePath, ProcessedFolderName, cancellationToken);

    public Task<string> MoveToQuarantineAsync(string sourcePath, CancellationToken cancellationToken) =>
        MoveAsync(sourcePath, QuarantineFolderName, cancellationToken);

    private Task<string> MoveAsync(string sourcePath, string targetFolderName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        cancellationToken.ThrowIfCancellationRequested();

        var targetFolder = Path.Combine(_options.Path, targetFolderName);
        Directory.CreateDirectory(targetFolder);

        var targetPath = Path.Combine(targetFolder, Path.GetFileName(sourcePath));
        File.Move(sourcePath, targetPath, overwrite: true);

        return Task.FromResult(targetPath);
    }
}
