using Microsoft.Extensions.Options;

namespace Enrollment.Console;

/// <summary>
/// Moves a completed enrollment file to <c>processed/</c> or <c>quarantine/</c>
/// directly under <see cref="EnrollmentWatchOptions.Path"/>, per the
/// <c>enrollment-watch-ingestion</c> spec's "Completion Archival" requirement.
/// </summary>
public sealed class EnrollmentWatchArchiver
{
    private const string ProcessedFolderName = "processed";
    private const string QuarantineFolderName = "quarantine";

    private readonly EnrollmentWatchOptions _options;

    public EnrollmentWatchArchiver(IOptions<EnrollmentWatchOptions> options)
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
