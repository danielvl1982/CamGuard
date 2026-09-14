using Microsoft.Extensions.Options;
using Monitoring.Infrastructure;

namespace Monitoring.UnitTests;

/// <summary>
/// Infrastructure tests for <see cref="FileSystemImageArchiver"/>, per the
/// <c>motion-image-ingestion</c> spec's "Completion Archival" requirement.
/// </summary>
public sealed class FileSystemImageArchiverTests
{
    private static FileSystemImageArchiver CreateArchiver(string root) =>
        new(Options.Create(new WatchFolderOptions { Path = root }));

    [Fact]
    public async Task MoveToProcessedAsync_moves_the_file_into_the_processed_subfolder()
    {
        string root = Directory.CreateTempSubdirectory("camguard-archiver-").FullName;
        try
        {
            string sourcePath = Path.Combine(root, "success.png");
            await File.WriteAllBytesAsync(sourcePath, [1, 2, 3]);
            FileSystemImageArchiver archiver = CreateArchiver(root);

            string archivedPath = await archiver.MoveToProcessedAsync(sourcePath, CancellationToken.None);

            Assert.Equal(Path.Combine(root, "processed", "success.png"), archivedPath);
            Assert.True(File.Exists(archivedPath));
            Assert.False(File.Exists(sourcePath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task MoveToQuarantineAsync_moves_the_file_into_the_quarantine_subfolder()
    {
        string root = Directory.CreateTempSubdirectory("camguard-archiver-").FullName;
        try
        {
            string sourcePath = Path.Combine(root, "failure.png");
            await File.WriteAllBytesAsync(sourcePath, [1, 2, 3]);
            FileSystemImageArchiver archiver = CreateArchiver(root);

            string archivedPath = await archiver.MoveToQuarantineAsync(sourcePath, CancellationToken.None);

            Assert.Equal(Path.Combine(root, "quarantine", "failure.png"), archivedPath);
            Assert.True(File.Exists(archivedPath));
            Assert.False(File.Exists(sourcePath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task MoveToProcessedAsync_creates_the_processed_subfolder_when_missing()
    {
        string root = Directory.CreateTempSubdirectory("camguard-archiver-").FullName;
        try
        {
            string sourcePath = Path.Combine(root, "first-run.png");
            await File.WriteAllBytesAsync(sourcePath, [1, 2, 3]);
            FileSystemImageArchiver archiver = CreateArchiver(root);

            Assert.False(Directory.Exists(Path.Combine(root, "processed")));

            await archiver.MoveToProcessedAsync(sourcePath, CancellationToken.None);

            Assert.True(Directory.Exists(Path.Combine(root, "processed")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
