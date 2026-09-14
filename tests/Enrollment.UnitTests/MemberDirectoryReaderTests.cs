using Enrollment.Application;
using Enrollment.Domain;
using Enrollment.PublicApi;
using Moq;
using Moq.AutoMock;

namespace Enrollment.UnitTests;

/// <summary>
/// Public API boundary tests for <see cref="MemberDirectoryReader"/> (task 9.5) — covers
/// <c>member-directory-query</c> spec's "Public API Boundary" scenario: a consumer holding only
/// <see cref="IMemberDirectoryReader"/> gets a match-or-no-match result, with the internal query
/// handler/repository chain exercised end-to-end behind it.
/// </summary>
public sealed class MemberDirectoryReaderTests
{
    private static float[] ValidVector(float firstComponent = 0.9f)
    {
        var vector = new float[FaceEmbedding.ExpectedDimension];
        vector[0] = firstComponent;
        return vector;
    }

    private static (IMemberDirectoryReader Reader, Mock<IMemberVectorRepository> RepositoryMock) CreateReader()
    {
        var mocker = new AutoMocker();
        var handler = mocker.CreateInstance<QueryByEmbeddingQueryHandler>();
        IMemberDirectoryReader reader = new MemberDirectoryReader(handler);
        return (reader, mocker.GetMock<IMemberVectorRepository>());
    }

    [Fact]
    public async Task Consumer_holding_only_the_reader_gets_the_closest_member_match()
    {
        (IMemberDirectoryReader reader, Mock<IMemberVectorRepository> repositoryMock) = CreateReader();
        var memberId = MemberId.New();
        repositoryMock
            .Setup(x => x.FindNearestAsync(It.IsAny<FaceEmbedding>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((memberId, 0.95));

        MemberMatch? match = await reader.FindBestMatchAsync(ValidVector(), CancellationToken.None);

        Assert.NotNull(match);
        Assert.Equal(memberId.Value, match!.MemberId);
        Assert.Equal(0.95, match.Score);
    }

    [Fact]
    public async Task Consumer_holding_only_the_reader_gets_an_explicit_null_for_no_match()
    {
        (IMemberDirectoryReader reader, Mock<IMemberVectorRepository> repositoryMock) = CreateReader();
        repositoryMock
            .Setup(x => x.FindNearestAsync(It.IsAny<FaceEmbedding>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((MemberId Id, double Score)?)null);

        MemberMatch? match = await reader.FindBestMatchAsync(ValidVector(), CancellationToken.None);

        Assert.Null(match);
    }
}
