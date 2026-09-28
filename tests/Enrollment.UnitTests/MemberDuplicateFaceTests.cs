using CSharpFunctionalExtensions;
using Enrollment.Application;
using Enrollment.Domain;
using Moq;

namespace Enrollment.UnitTests;

/// <summary>
/// Covers member-crud T3 (duplicate-face validation on alta): a candidate face
/// already enrolled to another member is rejected with the matched member's name
/// and nothing is persisted; the same member's own images never trigger the gate;
/// the threshold boundary behaves (strictly-below rejects); annulled members'
/// images still trigger the gate (OD2).
/// </summary>
public sealed class MemberDuplicateFaceTests
{
    private const double Threshold = 0.4;

    private static FaceEmbedding Embedding(float first, float second = 0f)
    {
        var v = new float[FaceEmbedding.ExpectedDimension];
        v[0] = first;
        v[1] = second;
        return FaceEmbedding.FromVector(v).Value;
    }

    /// <summary>
    /// Builds a unit vector at the given cosine distance from [1, 0, ...]:
    /// [c, sqrt(1-c^2), 0, ...] has cosine similarity c, hence distance 1-c.
    /// </summary>
    private static FaceEmbedding EmbeddingAtDistance(double distance)
    {
        var c = (float)(1.0 - distance);
        var v = new float[FaceEmbedding.ExpectedDimension];
        v[0] = c;
        v[1] = (float)Math.Sqrt(1.0 - (double)c * c);
        return FaceEmbedding.FromVector(v).Value;
    }

    private sealed class InMemoryImageRepository : IMemberImageRepository
    {
        private readonly List<MemberImage> _images = [];
        public int SaveCalls { get; private set; }

        public void Seed(MemberImage image) => _images.Add(image);

        public Task<MemberImageId> SaveAsync(
            MemberId memberId, Stream imageBytes, FaceEmbedding embedding, string fileExtension, CancellationToken cancellationToken)
        {
            SaveCalls++;
            var image = MemberImage.Rehydrate(MemberImageId.New(), memberId, "seed.jpg", embedding, DateTime.UtcNow);
            _images.Add(image);
            return Task.FromResult(image.Id);
        }

        public Task<IReadOnlyList<MemberImage>> ListByMemberAsync(MemberId memberId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MemberImage>>(_images.Where(i => i.MemberId == memberId).ToList());

        public Task<IReadOnlyList<MemberImage>> ListAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MemberImage>>(_images.ToList());

        public Task DeleteAsync(MemberImageId imageId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static MemberImage StoredImage(MemberId owner, FaceEmbedding embedding) =>
        MemberImage.Rehydrate(MemberImageId.New(), owner, "photo.jpg", embedding, DateTime.UtcNow);

    private static void SetupExtraction(Mock<IFaceEmbeddingExtractor> extractorMock, params FaceEmbedding[] embeddings)
    {
        var queue = new Queue<FaceEmbedding>(embeddings);
        extractorMock.Setup(x => x.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Success<FaceEmbedding, FaceExtractionError>(queue.Dequeue()));
    }

    [Fact]
    public async Task Alta_rejected_with_matched_name_and_zero_persistence_when_face_matches_other_member()
    {
        var bobId = MemberId.New();
        var bobEmbedding = Embedding(0.8f);
        var images = new InMemoryImageRepository();
        images.Seed(StoredImage(bobId, bobEmbedding));
        var validator = new MemberImageDuplicateValidator(Threshold);
        var extractorMock = new Mock<IFaceEmbeddingExtractor>();
        SetupExtraction(extractorMock, bobEmbedding);
        var memberRepoMock = new Mock<IMemberVectorRepository>();
        memberRepoMock.Setup(x => x.FindByIdAsync(bobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member.Rehydrate(bobId, "Bob Smith", bobEmbedding));
        var handler = new RegisterMemberCommandHandler(extractorMock.Object, memberRepoMock.Object, images, validator);
        var command = new RegisterMemberCommand("Alice Jones", [new MemoryStream([0xFF, 0xD8, 0xFF])]);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RegisterMemberError.MatchesOtherMember, result.Error.Error);
        Assert.Equal("Bob Smith", result.Error.MatchedMemberName);
        Assert.Equal(0, images.SaveCalls);
        memberRepoMock.Verify(
            x => x.SaveAsync(It.IsAny<Member>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Validator_excluding_owner_returns_no_match_for_same_member_images()
    {
        var ownerId = MemberId.New();
        var ownerEmbedding = Embedding(0.8f);
        var images = new InMemoryImageRepository();
        images.Seed(StoredImage(ownerId, ownerEmbedding));
        var validator = new MemberImageDuplicateValidator(Threshold);
        var existingImages = await images.ListAllAsync(CancellationToken.None);

        // T7's call shape: the owner's own near-identical image must not match.
        var match = await validator.FindMatchAsync(ownerEmbedding, existingImages, ownerId, CancellationToken.None);

        Assert.Null(match);
    }

    [Fact]
    public async Task Validator_returns_best_match_with_member_id_and_distance()
    {
        var nearId = MemberId.New();
        var farId = MemberId.New();
        var candidate = Embedding(1f);
        var images = new InMemoryImageRepository();
        images.Seed(StoredImage(farId, EmbeddingAtDistance(0.3)));
        images.Seed(StoredImage(nearId, EmbeddingAtDistance(0.1)));
        var validator = new MemberImageDuplicateValidator(Threshold);
        var existingImages = await images.ListAllAsync(CancellationToken.None);

        var match = await validator.FindMatchAsync(candidate, existingImages, ownerMemberId: null, CancellationToken.None);

        Assert.NotNull(match);
        Assert.Equal(nearId, match.MatchedMemberId);
        Assert.True(Math.Abs(match.Distance - 0.1) < 1e-6, $"expected ~0.1, got {match.Distance}");
    }

    [Fact]
    public async Task Alta_accepted_when_no_stored_image_is_within_threshold()
    {
        var otherId = MemberId.New();
        var images = new InMemoryImageRepository();
        images.Seed(StoredImage(otherId, EmbeddingAtDistance(0.41)));
        var validator = new MemberImageDuplicateValidator(Threshold);
        var candidate = Embedding(1f);
        var extractorMock = new Mock<IFaceEmbeddingExtractor>();
        SetupExtraction(extractorMock, candidate);
        var memberRepoMock = new Mock<IMemberVectorRepository>();
        var handler = new RegisterMemberCommandHandler(extractorMock.Object, memberRepoMock.Object, images, validator);
        var command = new RegisterMemberCommand("Alice Jones", [new MemoryStream([0xFF, 0xD8, 0xFF])]);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        memberRepoMock.Verify(
            x => x.SaveAsync(It.IsAny<Member>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(1, images.SaveCalls);
    }

    [Theory]
    [InlineData(0.39, true)]
    [InlineData(0.41, false)]
    public async Task Threshold_boundary_just_below_rejects_just_above_accepts(double distance, bool expectMatch)
    {
        var otherId = MemberId.New();
        var images = new InMemoryImageRepository();
        images.Seed(StoredImage(otherId, EmbeddingAtDistance(distance)));
        var validator = new MemberImageDuplicateValidator(Threshold);
        var candidate = Embedding(1f);
        var existingImages = await images.ListAllAsync(CancellationToken.None);

        var match = await validator.FindMatchAsync(candidate, existingImages, ownerMemberId: null, CancellationToken.None);

        Assert.Equal(expectMatch, match is not null);
    }

    [Fact]
    public async Task Alta_rejected_when_face_matches_annulled_member_image()
    {
        // OD2: matching considers ALL members' images including annulled ones —
        // re-registering an annulled person's face still flags the match, by name.
        var annulledId = MemberId.New();
        var annulledEmbedding = Embedding(0.8f);
        var images = new InMemoryImageRepository();
        images.Seed(StoredImage(annulledId, annulledEmbedding));
        var validator = new MemberImageDuplicateValidator(Threshold);
        var extractorMock = new Mock<IFaceEmbeddingExtractor>();
        SetupExtraction(extractorMock, annulledEmbedding);
        var memberRepoMock = new Mock<IMemberVectorRepository>();
        memberRepoMock.Setup(x => x.FindByIdAsync(annulledId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member.Rehydrate(annulledId, "Ann Nullified", annulledEmbedding, isAnnulled: true));
        var handler = new RegisterMemberCommandHandler(extractorMock.Object, memberRepoMock.Object, images, validator);
        var command = new RegisterMemberCommand("Alice Jones", [new MemoryStream([0xFF, 0xD8, 0xFF])]);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RegisterMemberError.MatchesOtherMember, result.Error.Error);
        Assert.Equal("Ann Nullified", result.Error.MatchedMemberName);
        Assert.Equal(0, images.SaveCalls);
        memberRepoMock.Verify(
            x => x.SaveAsync(It.IsAny<Member>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Alta_rejected_when_second_photo_matches_other_member_and_first_photo_stored_nothing()
    {
        var bobId = MemberId.New();
        var bobEmbedding = Embedding(0.8f);
        var images = new InMemoryImageRepository();
        images.Seed(StoredImage(bobId, bobEmbedding));
        var validator = new MemberImageDuplicateValidator(Threshold);
        var extractorMock = new Mock<IFaceEmbeddingExtractor>();
        SetupExtraction(extractorMock, Embedding(0.1f, 0.9f), bobEmbedding);
        var memberRepoMock = new Mock<IMemberVectorRepository>();
        memberRepoMock.Setup(x => x.FindByIdAsync(bobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member.Rehydrate(bobId, "Bob Smith", bobEmbedding));
        var handler = new RegisterMemberCommandHandler(extractorMock.Object, memberRepoMock.Object, images, validator);
        var command = new RegisterMemberCommand(
            "Alice Jones", [new MemoryStream([0xFF, 0xD8, 0xFF]), new MemoryStream([0xFF, 0xD8, 0xFF])]);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RegisterMemberError.MatchesOtherMember, result.Error.Error);
        Assert.Equal(0, images.SaveCalls);
        memberRepoMock.Verify(
            x => x.SaveAsync(It.IsAny<Member>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
