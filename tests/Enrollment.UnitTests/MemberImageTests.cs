using System.Diagnostics;
using Common;
using CSharpFunctionalExtensions;
using Enrollment.Application;
using Enrollment.Domain;
using Enrollment.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Moq;

namespace Enrollment.UnitTests;

/// <summary>
/// Covers member-crud T2 (image persistence + register wiring): repository
/// save/list/delete round-trip against a temp image root and temp SQLite file,
/// plus registration persisting N photos and failing atomically on store errors.
/// </summary>
public sealed class MemberImageTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _imageRoot;
    private readonly VectorStoreOptions _options;
    private readonly SqliteVecMemberImageRepository _images;
    private readonly SqliteVecMemberRepository _members;

    public MemberImageTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"camguard-images-{Guid.NewGuid():N}.db");
        _imageRoot = Path.Combine(Path.GetTempPath(), $"camguard-image-files-{Guid.NewGuid():N}");
        _options = new VectorStoreOptions
        {
            ConnectionString = $"Data Source={_dbPath}",
            MembersCollectionName = "members_test",
            MemberImagesCollectionName = "member_images_test",
            MemberImagesRoot = _imageRoot
        };
        var options = Options.Create(_options);
        var activitySource = new ActivitySource("Enrollment.UnitTests.Images");
        _images = new SqliteVecMemberImageRepository(options, activitySource);
        _members = new SqliteVecMemberRepository(options, activitySource);
    }

    private static FaceEmbedding ValidEmbedding(float first = 0.8f, float second = 0f)
    {
        var v = new float[FaceEmbedding.ExpectedDimension];
        v[0] = first;
        v[1] = second;
        return FaceEmbedding.FromVector(v).Value;
    }

    private static MemoryStream PhotoBytes(byte[]? content = null) =>
        new(content ?? [0xFF, 0xD8, 0xFF, 0x00, 0x01, 0x02]);

    private static IMemberImageDuplicateValidator NoDuplicateValidator()
    {
        var noDuplicateMock = new Mock<IMemberImageDuplicateValidator>();
        noDuplicateMock.Setup(x => x.FindMatchAsync(It.IsAny<FaceEmbedding>(), It.IsAny<MemberId?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DuplicateFaceMatch?)null);
        return noDuplicateMock.Object;
    }

    [Fact]
    public async Task SaveAsync_persists_file_on_disk_and_record_in_store()
    {
        var memberId = MemberId.New();
        var embedding = ValidEmbedding();

        var imageId = await _images.SaveAsync(memberId, PhotoBytes(), embedding, ".jpg", CancellationToken.None);

        var listed = await _images.ListByMemberAsync(memberId, CancellationToken.None);
        var single = Assert.Single(listed);
        Assert.Equal(imageId, single.Id);
        Assert.Equal(memberId, single.MemberId);
        Assert.Equal(imageId.Value.ToString() + ".jpg", single.FileName);
        Assert.Equal(embedding, single.Embedding);
        Assert.Equal(DateTimeKind.Utc, single.StoredAtUtc.Kind);
        Assert.True(DateTime.UtcNow - single.StoredAtUtc < TimeSpan.FromMinutes(1));

        var memberDir = Path.Combine(_imageRoot, memberId.Value.ToString());
        Assert.True(File.Exists(Path.Combine(memberDir, single.FileName)), "image file must exist on disk");
    }

    [Fact]
    public async Task SaveAsync_leaves_the_input_stream_readable_and_open()
    {
        using var photo = PhotoBytes();

        await _images.SaveAsync(MemberId.New(), photo, ValidEmbedding(), ".jpg", CancellationToken.None);

        Assert.True(photo.CanRead);
        Assert.True(photo.CanSeek);
    }

    [Fact]
    public async Task ListByMemberAsync_returns_only_the_requested_members_images()
    {
        var first = MemberId.New();
        var second = MemberId.New();
        await _images.SaveAsync(first, PhotoBytes(), ValidEmbedding(0.8f), ".jpg", CancellationToken.None);
        await _images.SaveAsync(first, PhotoBytes(), ValidEmbedding(0.7f), ".png", CancellationToken.None);
        await _images.SaveAsync(second, PhotoBytes(), ValidEmbedding(0.6f), ".jpg", CancellationToken.None);

        var listed = await _images.ListByMemberAsync(first, CancellationToken.None);

        Assert.Equal(2, listed.Count);
        Assert.All(listed, i => Assert.Equal(first, i.MemberId));
    }

    [Fact]
    public async Task ListAllAsync_returns_images_across_members_with_vectors()
    {
        var first = MemberId.New();
        var second = MemberId.New();
        var firstEmbedding = ValidEmbedding(0.8f);
        await _images.SaveAsync(first, PhotoBytes(), firstEmbedding, ".jpg", CancellationToken.None);
        await _images.SaveAsync(second, PhotoBytes(), ValidEmbedding(0.6f), ".jpg", CancellationToken.None);

        var all = await _images.ListAllAsync(CancellationToken.None);

        Assert.Equal(2, all.Count);
        Assert.Contains(all, i => i.MemberId == first && i.Embedding == firstEmbedding);
        Assert.Contains(all, i => i.MemberId == second);
    }

    [Fact]
    public async Task DeleteAsync_removes_both_record_and_file()
    {
        var memberId = MemberId.New();
        var imageId = await _images.SaveAsync(memberId, PhotoBytes(), ValidEmbedding(), ".jpg", CancellationToken.None);

        await _images.DeleteAsync(imageId, CancellationToken.None);

        Assert.Empty(await _images.ListByMemberAsync(memberId, CancellationToken.None));
        Assert.Empty(await _images.ListAllAsync(CancellationToken.None));
        var memberDir = Path.Combine(_imageRoot, memberId.Value.ToString());
        Assert.Empty(Directory.Exists(memberDir) ? Directory.GetFiles(memberDir) : []);
    }

    [Fact]
    public async Task DeleteAsync_unknown_id_throws_KeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _images.DeleteAsync(MemberImageId.New(), CancellationToken.None));
    }

    [Fact]
    public async Task Register_persists_every_photo_as_file_plus_record_and_leaves_streams_readable()
    {
        var extractorMock = new Mock<IFaceEmbeddingExtractor>();
        extractorMock.Setup(x => x.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<FaceEmbedding, FaceExtractionError>(ValidEmbedding()));
        var memberRepoMock = new Mock<IMemberVectorRepository>();
        var duplicateValidator = new MemberImageDuplicateValidator(_images, _options.DuplicateFaceDistanceThreshold);
        var handler = new RegisterMemberCommandHandler(extractorMock.Object, memberRepoMock.Object, _images, duplicateValidator);
        var firstPhoto = PhotoBytes();
        var secondPhoto = PhotoBytes();
        var command = new RegisterMemberCommand("Ada Lovelace", [firstPhoto, secondPhoto]);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        memberRepoMock.Verify(
            x => x.SaveAsync(It.Is<Member>(m => m.FullName == "Ada Lovelace"), It.IsAny<CancellationToken>()),
            Times.Once);
        var images = await _images.ListByMemberAsync(result.Value, CancellationToken.None);
        Assert.Equal(2, images.Count);
        var memberDir = Path.Combine(_imageRoot, result.Value.Value.ToString());
        Assert.Equal(2, Directory.GetFiles(memberDir).Length);
        Assert.All(images, i => Assert.Equal(result.Value, i.MemberId));
        Assert.True(firstPhoto.CanRead);
        Assert.True(secondPhoto.CanRead);
        Assert.Equal(0, firstPhoto.Position);
        Assert.Equal(0, secondPhoto.Position);
    }

    [Fact]
    public async Task Register_with_store_failure_fails_with_ImageStoreFailed_and_persists_no_member()
    {
        var extractorMock = new Mock<IFaceEmbeddingExtractor>();
        extractorMock.Setup(x => x.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<FaceEmbedding, FaceExtractionError>(ValidEmbedding()));
        var imageRepoMock = new Mock<IMemberImageRepository>();
        imageRepoMock.Setup(x => x.SaveAsync(
                It.IsAny<MemberId>(), It.IsAny<Stream>(), It.IsAny<FaceEmbedding>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("disk full"));
        var memberRepoMock = new Mock<IMemberVectorRepository>();
        var noDuplicateMock = new Mock<IMemberImageDuplicateValidator>();
        noDuplicateMock.Setup(x => x.FindMatchAsync(It.IsAny<FaceEmbedding>(), It.IsAny<MemberId?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DuplicateFaceMatch?)null);
        var handler = new RegisterMemberCommandHandler(extractorMock.Object, memberRepoMock.Object, imageRepoMock.Object, noDuplicateMock.Object);
        var command = new RegisterMemberCommand("Ada Lovelace", [PhotoBytes()]);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RegisterMemberError.ImageStoreFailed, result.Error.Error);
        memberRepoMock.Verify(
            x => x.SaveAsync(It.IsAny<Member>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Register_with_store_failure_leaves_no_member_in_a_real_store()
    {
        var extractorMock = new Mock<IFaceEmbeddingExtractor>();
        extractorMock.Setup(x => x.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<FaceEmbedding, FaceExtractionError>(ValidEmbedding()));
        var imageRepoMock = new Mock<IMemberImageRepository>();
        imageRepoMock.Setup(x => x.SaveAsync(
                It.IsAny<MemberId>(), It.IsAny<Stream>(), It.IsAny<FaceEmbedding>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("disk full"));
        var handler = new RegisterMemberCommandHandler(extractorMock.Object, _members, imageRepoMock.Object, NoDuplicateValidator());
        var command = new RegisterMemberCommand("Ada Lovelace", [PhotoBytes()]);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RegisterMemberError.ImageStoreFailed, result.Error.Error);
        Assert.Null(await _members.FindByFullNameAsync("Ada Lovelace", CancellationToken.None));
        Assert.Empty(await _members.ListAllAsync(CancellationToken.None));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch (IOException) { }
        }

        if (Directory.Exists(_imageRoot))
        {
            try { Directory.Delete(_imageRoot, recursive: true); } catch (IOException) { }
        }
    }
}
