using CSharpFunctionalExtensions;
using Enrollment.Application;
using Enrollment.Console;
using Enrollment.Domain;
using Microsoft.Extensions.Options;
using Moq;

namespace Enrollment.UnitTests;

/// <summary>
/// Covers enrollment-watch-ingestion spec: Completion Archival — successes to processed/,
/// failures (validation, NoFace, MultipleFaces) to quarantine/. Also covers the watch-level
/// routing that Program.cs performs after EnrollmentService returns.
/// </summary>
public sealed class EnrollmentWatchArchiverTests
{
    private static string CreateTempEnrollRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"camguard-archiver-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static EnrollmentWatchArchiver CreateArchiver(string enrollRoot)
    {
        var options = Options.Create(new EnrollmentWatchOptions { Path = enrollRoot });
        return new EnrollmentWatchArchiver(options);
    }

    private static FaceEmbedding ValidEmbedding(float first = 0.8f)
    {
        var v = new float[FaceEmbedding.ExpectedDimension];
        v[0] = first;
        return FaceEmbedding.FromVector(v).Value;
    }

    [Fact]
    public async Task MoveToProcessedAsync_moves_successful_enrollment_to_processed_folder()
    {
        // Scenario: Success archived to processed — GIVEN stable file with valid FullName and exactly one face WHEN enrollment succeeds THEN moves to photos/enroll/processed/
        var enrollRoot = CreateTempEnrollRoot();
        try
        {
            var archiver = CreateArchiver(enrollRoot);
            var file = Path.Combine(enrollRoot, "success.jpg");
            await File.WriteAllBytesAsync(file, [1, 2, 3]);

            // Simulate EnrollmentService success path (as Program.cs does)
            var extractor = new Mock<IFaceEmbeddingExtractor>();
            extractor.Setup(x => x.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success<FaceEmbedding, FaceExtractionError>(ValidEmbedding()));
            var repo = new Mock<IMemberVectorRepository>();
            repo.Setup(x => x.FindByFullNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Member?)null);
            var service = new EnrollmentService(extractor.Object, repo.Object);
            Result<MemberId, RegisterMemberError> result;
            await using (var stream = File.OpenRead(file))
            {
                result = await service.EnrollAsync("Ana Torres", stream, CancellationToken.None);
            }
            Assert.True(result.IsSuccess);

            // Archival for success → processed (mirrors Program.RunWatchAsync success branch) — stream must be closed before File.Move
            var processedPath = await archiver.MoveToProcessedAsync(file, CancellationToken.None);

            Assert.Equal(Path.Combine(enrollRoot, "processed", "success.jpg"), processedPath);
            Assert.True(File.Exists(processedPath));
            Assert.False(File.Exists(file));
            Assert.True(Directory.Exists(Path.Combine(enrollRoot, "processed")));
        }
        finally
        {
            Directory.Delete(enrollRoot, recursive: true);
        }
    }

    [Fact]
    public async Task MoveToQuarantineAsync_moves_file_on_InvalidName()
    {
        // Scenario: Empty FullName rejected — GIVEN stable file prompted and user enters "" WHEN validation runs THEN rejected with InvalidName AND moves to quarantine
        // Also covers member-enrollment spec: Enrollment Name Validation — empty rejected
        var enrollRoot = CreateTempEnrollRoot();
        try
        {
            var archiver = CreateArchiver(enrollRoot);
            var file = Path.Combine(enrollRoot, "invalid.jpg");
            await File.WriteAllBytesAsync(file, [1, 2, 3]);

            var extractor = new Mock<IFaceEmbeddingExtractor>();
            var repo = new Mock<IMemberVectorRepository>();
            var service = new EnrollmentService(extractor.Object, repo.Object);

            Result<MemberId, RegisterMemberError> result;
            await using (var stream = File.OpenRead(file))
            {
                result = await service.EnrollAsync("", stream, CancellationToken.None);
            }
            Assert.True(result.IsFailure);
            Assert.Equal(RegisterMemberError.InvalidName, result.Error);
            repo.Verify(x => x.SaveAsync(It.IsAny<Member>(), It.IsAny<CancellationToken>()), Times.Never);

            // Program quarantines on InvalidName (and archiver is called) — stream closed before move
            var quarantined = await archiver.MoveToQuarantineAsync(file, CancellationToken.None);

            Assert.Equal(Path.Combine(enrollRoot, "quarantine", "invalid.jpg"), quarantined);
            Assert.True(File.Exists(quarantined));
            Assert.False(File.Exists(file));
        }
        finally
        {
            Directory.Delete(enrollRoot, recursive: true);
        }
    }

    [Fact]
    public async Task MoveToQuarantineAsync_moves_file_on_NoFaceDetected()
    {
        // Scenario: NoFace archived to quarantine — GIVEN photo has zero faces WHEN processed THEN no record persisted and file moves to quarantine
        var enrollRoot = CreateTempEnrollRoot();
        try
        {
            var archiver = CreateArchiver(enrollRoot);
            var file = Path.Combine(enrollRoot, "noface.jpg");
            await File.WriteAllBytesAsync(file, [1, 2, 3]);

            var extractor = new Mock<IFaceEmbeddingExtractor>();
            extractor.Setup(x => x.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Failure<FaceEmbedding, FaceExtractionError>(FaceExtractionError.NoFaceDetected));
            var repo = new Mock<IMemberVectorRepository>();
            repo.Setup(x => x.FindByFullNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Member?)null);
            var service = new EnrollmentService(extractor.Object, repo.Object);

            Result<MemberId, RegisterMemberError> result;
            await using (var stream = File.OpenRead(file))
            {
                result = await service.EnrollAsync("Ana Torres", stream, CancellationToken.None);
            }
            Assert.True(result.IsFailure);
            Assert.Equal(RegisterMemberError.NoFaceDetected, result.Error);
            repo.Verify(x => x.SaveAsync(It.IsAny<Member>(), It.IsAny<CancellationToken>()), Times.Never);

            var quarantined = await archiver.MoveToQuarantineAsync(file, CancellationToken.None);
            Assert.True(File.Exists(quarantined));
            Assert.False(File.Exists(file));
        }
        finally
        {
            Directory.Delete(enrollRoot, recursive: true);
        }
    }

    [Fact]
    public async Task MoveToQuarantineAsync_moves_file_on_MultipleFacesDetected()
    {
        // Scenario: MultipleFaces archived to quarantine — GIVEN photo has two+ faces WHEN processed THEN no record persisted and moves to quarantine
        var enrollRoot = CreateTempEnrollRoot();
        try
        {
            var archiver = CreateArchiver(enrollRoot);
            var file = Path.Combine(enrollRoot, "multi.jpg");
            await File.WriteAllBytesAsync(file, [1, 2, 3]);

            var extractor = new Mock<IFaceEmbeddingExtractor>();
            extractor.Setup(x => x.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Failure<FaceEmbedding, FaceExtractionError>(FaceExtractionError.MultipleFacesDetected));
            var repo = new Mock<IMemberVectorRepository>();
            repo.Setup(x => x.FindByFullNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Member?)null);
            var service = new EnrollmentService(extractor.Object, repo.Object);

            Result<MemberId, RegisterMemberError> result;
            await using (var stream = File.OpenRead(file))
            {
                result = await service.EnrollAsync("Ana Torres", stream, CancellationToken.None);
            }
            Assert.True(result.IsFailure);
            Assert.Equal(RegisterMemberError.MultipleFacesDetected, result.Error);
            repo.Verify(x => x.SaveAsync(It.IsAny<Member>(), It.IsAny<CancellationToken>()), Times.Never);

            var quarantined = await archiver.MoveToQuarantineAsync(file, CancellationToken.None);
            Assert.True(File.Exists(quarantined));
            Assert.False(File.Exists(file));
        }
        finally
        {
            Directory.Delete(enrollRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Archiver_does_not_leave_completed_file_in_drop_folder()
    {
        // Scenario: Completion Archival — MUST NOT leave completed files in drop folder
        var enrollRoot = CreateTempEnrollRoot();
        try
        {
            var archiver = CreateArchiver(enrollRoot);
            var successFile = Path.Combine(enrollRoot, "a.jpg");
            var failFile = Path.Combine(enrollRoot, "b.jpg");
            await File.WriteAllBytesAsync(successFile, [1]);
            await File.WriteAllBytesAsync(failFile, [2]);

            await archiver.MoveToProcessedAsync(successFile, CancellationToken.None);
            await archiver.MoveToQuarantineAsync(failFile, CancellationToken.None);

            var remainingInDrop = Directory.GetFiles(enrollRoot, "*", SearchOption.TopDirectoryOnly);
            Assert.Empty(remainingInDrop);
            Assert.True(File.Exists(Path.Combine(enrollRoot, "processed", "a.jpg")));
            Assert.True(File.Exists(Path.Combine(enrollRoot, "quarantine", "b.jpg")));
        }
        finally
        {
            Directory.Delete(enrollRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Archiver_creates_processed_and_quarantine_folders_if_missing()
    {
        var enrollRoot = CreateTempEnrollRoot();
        try
        {
            // Remove subfolders if existed
            var processed = Path.Combine(enrollRoot, "processed");
            var quarantine = Path.Combine(enrollRoot, "quarantine");
            if (Directory.Exists(processed)) Directory.Delete(processed);
            if (Directory.Exists(quarantine)) Directory.Delete(quarantine);

            var archiver = CreateArchiver(enrollRoot);
            var f1 = Path.Combine(enrollRoot, "x.jpg");
            var f2 = Path.Combine(enrollRoot, "y.jpg");
            await File.WriteAllBytesAsync(f1, [1]);
            await File.WriteAllBytesAsync(f2, [2]);

            await archiver.MoveToProcessedAsync(f1, CancellationToken.None);
            await archiver.MoveToQuarantineAsync(f2, CancellationToken.None);

            Assert.True(Directory.Exists(processed));
            Assert.True(Directory.Exists(quarantine));
        }
        finally
        {
            Directory.Delete(enrollRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Archiver_invoked_via_Program_logic_routes_InvalidName_to_quarantine_and_success_to_processed()
    {
        // Integration of EnrollmentService failure/success with archiver as Program.RunWatchAsync does:
        // empty → quarantine InvalidName, success → processed
        var enrollRoot = CreateTempEnrollRoot();
        try
        {
            var archiver = CreateArchiver(enrollRoot);
            var extractorSuccess = new Mock<IFaceEmbeddingExtractor>();
            extractorSuccess.Setup(x => x.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success<FaceEmbedding, FaceExtractionError>(ValidEmbedding()));
            var repo = new Mock<IMemberVectorRepository>();
            repo.Setup(x => x.FindByFullNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Member?)null);
            var service = new EnrollmentService(extractorSuccess.Object, repo.Object);

            var validFile = Path.Combine(enrollRoot, "valid.jpg");
            await File.WriteAllBytesAsync(validFile, [1]);
            Result<MemberId, RegisterMemberError> r1;
            await using (var s = File.OpenRead(validFile))
            {
                r1 = await service.EnrollAsync("Jose Garcia", s, CancellationToken.None);
            }
            if (r1.IsSuccess)
                await archiver.MoveToProcessedAsync(validFile, CancellationToken.None);
            else
                await archiver.MoveToQuarantineAsync(validFile, CancellationToken.None);
            Assert.True(File.Exists(Path.Combine(enrollRoot, "processed", "valid.jpg")));

            var invalidFile = Path.Combine(enrollRoot, "invalid2.jpg");
            await File.WriteAllBytesAsync(invalidFile, [1]);
            Result<MemberId, RegisterMemberError> r2;
            await using (var s = File.OpenRead(invalidFile))
            {
                r2 = await service.EnrollAsync("   ", s, CancellationToken.None);
            }
            if (r2.IsSuccess)
                await archiver.MoveToProcessedAsync(invalidFile, CancellationToken.None);
            else
                await archiver.MoveToQuarantineAsync(invalidFile, CancellationToken.None);
            Assert.True(File.Exists(Path.Combine(enrollRoot, "quarantine", "invalid2.jpg")));
        }
        finally
        {
            Directory.Delete(enrollRoot, recursive: true);
        }
    }
}
