using System.Diagnostics;
using Enrollment.PublicApi;
using Microsoft.Agents.AI.Workflows;
using Moq;
using Moq.AutoMock;
using Monitoring.Application;

namespace Monitoring.UnitTests;

/// <summary>
/// Tests for <see cref="VerifyIdentityExecutor"/>, per the <c>identity-verification</c> spec:
/// queries <see cref="IMemberDirectoryReader"/> and forwards <see cref="MemberMatch.Score"/>
/// bit-for-bit as <c>Distance</c> — never rounded, truncated, or reinterpreted as similarity.
///
/// <para>
/// Embeddings used here are entirely synthetic/mocked short arrays — <see cref="IMemberDirectoryReader"/>
/// is mocked directly, so this executor never touches <c>Enrollment.Domain.FaceEmbedding</c> or its
/// (still-unconfirmed) <c>ExpectedDimension</c>; these tests deliberately do not depend on that
/// value being 512 or any other specific length.
/// </para>
/// </summary>
public sealed class VerifyIdentityExecutorTests
{
    private static MonitoringWorkflowContext CreateInput(float[] embedding) =>
        new("image.png", Stream.Null, embedding, CandidateMemberId: null, Distance: null, Decision: null);

    private static AutoMocker CreateMocker()
    {
        var mocker = new AutoMocker();
        mocker.Use(new ActivitySource("Test"));
        return mocker;
    }

    [Fact]
    public async Task Detected_embedding_triggers_a_directory_query()
    {
        AutoMocker mocker = CreateMocker();
        float[] embedding = [0.1f, 0.2f, 0.3f];
        mocker.GetMock<IMemberDirectoryReader>()
            .Setup(x => x.FindBestMatchAsync(embedding, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemberMatch?)null);
        var executor = mocker.CreateInstance<VerifyIdentityExecutor>();

        await executor.HandleAsync(CreateInput(embedding), Mock.Of<IWorkflowContext>(), CancellationToken.None);

        mocker.GetMock<IMemberDirectoryReader>()
            .Verify(x => x.FindBestMatchAsync(embedding, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_low_Score_is_forwarded_as_a_low_Distance_never_inverted_into_similarity()
    {
        AutoMocker mocker = CreateMocker();
        var memberId = Guid.NewGuid();
        const double lowDistanceCloseMatch = 0.05;
        mocker.GetMock<IMemberDirectoryReader>()
            .Setup(x => x.FindBestMatchAsync(It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberMatch(memberId, lowDistanceCloseMatch));
        var executor = mocker.CreateInstance<VerifyIdentityExecutor>();

        MonitoringWorkflowContext result = await executor.HandleAsync(CreateInput([0.4f]), Mock.Of<IWorkflowContext>(), CancellationToken.None);

        // A low value must remain a low DISTANCE (close match) — never reinterpreted/inverted as a
        // low similarity, which would wrongly mean a far match.
        Assert.Equal(lowDistanceCloseMatch, result.Distance);
        Assert.Equal(memberId, result.CandidateMemberId);
    }

    [Fact]
    public async Task No_match_forwards_a_null_distance_and_null_candidate()
    {
        AutoMocker mocker = CreateMocker();
        mocker.GetMock<IMemberDirectoryReader>()
            .Setup(x => x.FindBestMatchAsync(It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemberMatch?)null);
        var executor = mocker.CreateInstance<VerifyIdentityExecutor>();

        MonitoringWorkflowContext result = await executor.HandleAsync(CreateInput([0.4f]), Mock.Of<IWorkflowContext>(), CancellationToken.None);

        Assert.Null(result.Distance);
        Assert.Null(result.CandidateMemberId);
    }

    [Fact]
    public async Task Boundary_distance_value_is_forwarded_bit_for_bit_exact()
    {
        AutoMocker mocker = CreateMocker();
        const double boundaryDistance = 0.34999999999999998; // deliberately not a "round" number
        mocker.GetMock<IMemberDirectoryReader>()
            .Setup(x => x.FindBestMatchAsync(It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberMatch(Guid.NewGuid(), boundaryDistance));
        var executor = mocker.CreateInstance<VerifyIdentityExecutor>();

        MonitoringWorkflowContext result = await executor.HandleAsync(CreateInput([0.1f]), Mock.Of<IWorkflowContext>(), CancellationToken.None);

        Assert.Equal(
            BitConverter.DoubleToInt64Bits(boundaryDistance),
            BitConverter.DoubleToInt64Bits(result.Distance!.Value));
    }
}
