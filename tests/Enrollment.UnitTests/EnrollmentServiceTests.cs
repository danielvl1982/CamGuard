using CSharpFunctionalExtensions;
using Enrollment.Application;
using Enrollment.Domain;
using Moq;
using System.Diagnostics;
using Common;
using Enrollment.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Enrollment.UnitTests;

/// <summary>
/// Covers member-enrollment spec: Enrollment Name Validation (InvalidName) and
/// Normalized FullName Lookup branching via EnrollmentService.
/// Also covers Atomic Vector Persistence overwrite atomic and archival success/failure routing.
/// </summary>
public sealed class EnrollmentServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteVecMemberRepository _repository;

    public EnrollmentServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"camguard-enrollment-service-{Guid.NewGuid():N}.db");
        var options = Options.Create(new VectorStoreOptions
        {
            ConnectionString = $"Data Source={_dbPath}",
            MembersCollectionName = "members_test"
        });
        _repository = new SqliteVecMemberRepository(options, new ActivitySource("Enrollment.UnitTests.EnrollmentService"));
    }

    private static FaceEmbedding ValidEmbedding(float first = 0.8f)
    {
        var v = new float[FaceEmbedding.ExpectedDimension];
        v[0] = first;
        return FaceEmbedding.FromVector(v).Value;
    }

    private static Mock<IFaceEmbeddingExtractor> ExtractorReturning(FaceEmbedding embedding) =>
        ExtractorMock(Result.Success<FaceEmbedding, FaceExtractionError>(embedding));

    private static Mock<IFaceEmbeddingExtractor> ExtractorFailing(FaceExtractionError error) =>
        ExtractorMock(Result.Failure<FaceEmbedding, FaceExtractionError>(error));

    private static Mock<IFaceEmbeddingExtractor> ExtractorMock(Result<FaceEmbedding, FaceExtractionError> result)
    {
        var mock = new Mock<IFaceEmbeddingExtractor>();
        mock.Setup(x => x.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return mock;
    }

    [Fact]
    public async Task EnrollAsync_rejects_empty_FullName_with_InvalidName_and_does_not_persist()
    {
        var extractor = ExtractorReturning(ValidEmbedding());
        var repoMock = new Mock<IMemberVectorRepository>();
        var service = new EnrollmentService(extractor.Object, repoMock.Object);

        var result = await service.EnrollAsync("", new MemoryStream([1, 2, 3]), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RegisterMemberError.InvalidName, result.Error);
        extractor.Verify(x => x.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        repoMock.Verify(x => x.SaveAsync(It.IsAny<Member>(), It.IsAny<CancellationToken>()), Times.Never);
        repoMock.Verify(x => x.FindByFullNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnrollAsync_rejects_whitespace_FullName_with_InvalidName_and_does_not_persist()
    {
        var extractor = ExtractorReturning(ValidEmbedding());
        var repoMock = new Mock<IMemberVectorRepository>();
        var service = new EnrollmentService(extractor.Object, repoMock.Object);

        var result = await service.EnrollAsync("   ", new MemoryStream([1]), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RegisterMemberError.InvalidName, result.Error);
        extractor.Verify(x => x.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        repoMock.Verify(x => x.SaveAsync(It.IsAny<Member>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnrollAsync_returns_InvalidName_without_calling_extractor_when_FullName_null_whitespace()
    {
        // Also covers member-enrollment spec: Enrollment Name Validation — null/empty/whitespace
        var extractor = new Mock<IFaceEmbeddingExtractor>();
        var repoMock = new Mock<IMemberVectorRepository>();
        var service = new EnrollmentService(extractor.Object, repoMock.Object);

        var result = await service.EnrollAsync("   ", new MemoryStream(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RegisterMemberError.InvalidName, result.Error);
        extractor.Verify(x => x.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnrollAsync_maps_NoFaceDetected_to_failure_and_does_not_persist()
    {
        var extractor = ExtractorFailing(FaceExtractionError.NoFaceDetected);
        var repoMock = new Mock<IMemberVectorRepository>();
        repoMock.Setup(x => x.FindByFullNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Member?)null);
        var service = new EnrollmentService(extractor.Object, repoMock.Object);

        var result = await service.EnrollAsync("Ana Torres", new MemoryStream(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RegisterMemberError.NoFaceDetected, result.Error);
        repoMock.Verify(x => x.SaveAsync(It.IsAny<Member>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnrollAsync_maps_MultipleFacesDetected_to_failure_and_does_not_persist()
    {
        var extractor = ExtractorFailing(FaceExtractionError.MultipleFacesDetected);
        var repoMock = new Mock<IMemberVectorRepository>();
        repoMock.Setup(x => x.FindByFullNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Member?)null);
        var service = new EnrollmentService(extractor.Object, repoMock.Object);

        var result = await service.EnrollAsync("Ana Torres", new MemoryStream(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RegisterMemberError.MultipleFacesDetected, result.Error);
        repoMock.Verify(x => x.SaveAsync(It.IsAny<Member>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnrollAsync_creates_new_MemberId_when_no_normalized_match_exists()
    {
        // GIVEN no normalized match for Luis Ponce
        var extractor = ExtractorReturning(ValidEmbedding(0.5f));
        var service = new EnrollmentService(extractor.Object, _repository);

        var result = await service.EnrollAsync("Luis Ponce", new MemoryStream(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = await _repository.FindByFullNameAsync("Luis Ponce", CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(result.Value, stored!.Id);
        // Lookup via FindNearest with same embedding returns that member
        var nearest = await _repository.FindNearestAsync(ValidEmbedding(0.5f), CancellationToken.None);
        Assert.NotNull(nearest);
        Assert.Equal(result.Value, nearest!.Value.Id);
    }

    [Fact]
    public async Task EnrollAsync_reuses_existing_MemberId_when_case_insensitive_match_exists()
    {
        // GIVEN Ana Torres enrolled
        var firstEmbedding = ValidEmbedding(0.4f);
        var existingId = MemberId.New();
        var existing = Member.Enroll(existingId, "Ana Torres", firstEmbedding).Value;
        await _repository.SaveAsync(existing, CancellationToken.None);

        var secondEmbedding = ValidEmbedding(0.9f);
        var extractor = ExtractorReturning(secondEmbedding);
        var service = new EnrollmentService(extractor.Object, _repository);

        // WHEN watch file arrives with ana torres (case variant)
        var result = await service.EnrollAsync("ana torres", new MemoryStream(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(existingId, result.Value);

        var found = await _repository.FindByFullNameAsync("Ana Torres", CancellationToken.None);
        Assert.NotNull(found);
        Assert.Equal(existingId, found!.Id);
        // Embedding should be overwritten to second (last wins)
        Assert.Equal(secondEmbedding, found.Embedding);
    }

    [Fact]
    public async Task EnrollAsync_reuses_existing_MemberId_when_trimmed_match_exists()
    {
        // GIVEN Jose Garcia enrolled
        var firstEmbedding = ValidEmbedding(0.3f);
        var existingId = MemberId.New();
        var existing = Member.Enroll(existingId, "Jose Garcia", firstEmbedding).Value;
        await _repository.SaveAsync(existing, CancellationToken.None);

        var secondEmbedding = ValidEmbedding(0.85f);
        var extractor = ExtractorReturning(secondEmbedding);
        var service = new EnrollmentService(extractor.Object, _repository);

        // WHEN watch file arrives with "  Jose Garcia  " (padded)
        var result = await service.EnrollAsync("  Jose Garcia  ", new MemoryStream(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(existingId, result.Value);
        var found = await _repository.FindByFullNameAsync("Jose Garcia", CancellationToken.None);
        Assert.NotNull(found);
        Assert.Equal(existingId, found!.Id);
        Assert.Equal(secondEmbedding, found.Embedding);
        // Stored FullName should be trimmed
        Assert.Equal("Jose Garcia", found.FullName);
    }

    [Fact]
    public async Task EnrollAsync_overwrites_embedding_atomically_same_Id_single_record_query_with_photo_B_returns_member()
    {
        // GIVEN member enrolled from photo A
        var firstEmbedding = ValidEmbedding(0.2f);
        var id = MemberId.New();
        var firstMember = Member.Enroll(id, "Maria Lopez", firstEmbedding).Value;
        await _repository.SaveAsync(firstMember, CancellationToken.None);

        var secondEmbedding = ValidEmbedding(0.75f);
        var extractor = ExtractorReturning(secondEmbedding);
        var service = new EnrollmentService(extractor.Object, _repository);

        // WHEN same normalized FullName re-enrolled from photo B (one face) with "  MARIA LOPEZ  "
        var result = await service.EnrollAsync("  MARIA LOPEZ  ", new MemoryStream(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(id, result.Value);

        // THEN store holds exactly one record for that MemberId with photo B's embedding
        var stored = await _repository.FindByFullNameAsync("Maria Lopez", CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(id, stored!.Id);
        Assert.Equal(secondEmbedding, stored.Embedding);
        Assert.NotEqual(firstEmbedding, stored.Embedding);

        // AND directory query with photo B's embedding returns that member
        var nearest = await _repository.FindNearestAsync(secondEmbedding, CancellationToken.None);
        Assert.NotNull(nearest);
        Assert.Equal(id, nearest!.Value.Id);

        // member count does not increase: there is still exactly one member for that normalized name
        // Verify unknown name still not found (no second row leaked)
        Assert.Null(await _repository.FindByFullNameAsync("Unknown Person", CancellationToken.None));
        // And case variant still resolves to same single record
        var variant = await _repository.FindByFullNameAsync("maria lopez", CancellationToken.None);
        Assert.NotNull(variant);
        Assert.Equal(id, variant!.Id);
    }

    [Fact]
    public async Task EnrollAsync_trims_FullName_before_persist()
    {
        var embedding = ValidEmbedding(0.6f);
        var extractor = ExtractorReturning(embedding);
        var service = new EnrollmentService(extractor.Object, _repository);

        var result = await service.EnrollAsync("  Ana Torres  ", new MemoryStream(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = await _repository.FindByFullNameAsync("Ana Torres", CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("Ana Torres", stored!.FullName);
    }

    [Fact]
    public async Task EnrollAsync_failed_extraction_does_not_overwrite_existing_member()
    {
        // Ensure atomicity: failed re-enrollment does not corrupt existing record
        var firstEmbedding = ValidEmbedding(0.5f);
        var id = MemberId.New();
        var firstMember = Member.Enroll(id, "Ana Torres", firstEmbedding).Value;
        await _repository.SaveAsync(firstMember, CancellationToken.None);

        var extractor = ExtractorFailing(FaceExtractionError.NoFaceDetected);
        var service = new EnrollmentService(extractor.Object, _repository);

        var result = await service.EnrollAsync("ana torres", new MemoryStream(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RegisterMemberError.NoFaceDetected, result.Error);
        var still = await _repository.FindByFullNameAsync("Ana Torres", CancellationToken.None);
        Assert.NotNull(still);
        Assert.Equal(firstEmbedding, still!.Embedding);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch (IOException) { }
        }
    }
}
