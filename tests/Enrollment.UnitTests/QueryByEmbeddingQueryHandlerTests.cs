using Enrollment.Application;
using Enrollment.Domain;
using Moq;
using Moq.AutoMock;

namespace Enrollment.UnitTests;

/// <summary>
/// Application handler tests covering <c>member-directory-query</c> spec scenarios:
/// best match, determinism, no-match, and round-trip (task 9.4).
/// </summary>
public sealed class QueryByEmbeddingQueryHandlerTests
{
    private static float[] ValidVector(float firstComponent = 0.9f)
    {
        var vector = new float[FaceEmbedding.ExpectedDimension];
        vector[0] = firstComponent;
        return vector;
    }

    [Fact]
    public async Task Query_returns_the_closest_enrolled_member_with_its_score()
    {
        var mocker = new AutoMocker();
        var memberId = MemberId.New();
        mocker.GetMock<IMemberVectorRepository>()
            .Setup(x => x.FindNearestAsync(It.IsAny<FaceEmbedding>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((memberId, 0.97));
        var handler = mocker.CreateInstance<QueryByEmbeddingQueryHandler>();

        var result = await handler.HandleAsync(new QueryByEmbeddingQuery(ValidVector()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(memberId, result.Value!.Value.Id);
        Assert.Equal(0.97, result.Value.Value.Score);
    }

    [Fact]
    public async Task Repeated_query_with_the_same_embedding_yields_the_identical_result()
    {
        var mocker = new AutoMocker();
        var memberId = MemberId.New();
        mocker.GetMock<IMemberVectorRepository>()
            .Setup(x => x.FindNearestAsync(It.IsAny<FaceEmbedding>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((memberId, 0.88));
        var handler = mocker.CreateInstance<QueryByEmbeddingQueryHandler>();
        var query = new QueryByEmbeddingQuery(ValidVector());

        var first = await handler.HandleAsync(query, CancellationToken.None);
        var second = await handler.HandleAsync(query, CancellationToken.None);

        Assert.Equal(first.Value, second.Value);
    }

    [Fact]
    public async Task Query_against_an_empty_directory_returns_an_explicit_no_match_result()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IMemberVectorRepository>()
            .Setup(x => x.FindNearestAsync(It.IsAny<FaceEmbedding>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((MemberId Id, double Score)?)null);
        var handler = mocker.CreateInstance<QueryByEmbeddingQueryHandler>();

        var result = await handler.HandleAsync(new QueryByEmbeddingQuery(ValidVector()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task Query_with_an_invalid_embedding_dimension_fails_without_querying_the_repository()
    {
        var mocker = new AutoMocker();
        var handler = mocker.CreateInstance<QueryByEmbeddingQueryHandler>();

        var result = await handler.HandleAsync(new QueryByEmbeddingQuery(new float[10]), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QueryByEmbeddingError.InvalidEmbedding, result.Error);
        mocker.GetMock<IMemberVectorRepository>()
            .Verify(x => x.FindNearestAsync(It.IsAny<FaceEmbedding>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
