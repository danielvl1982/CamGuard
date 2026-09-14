using System.Diagnostics;
using Microsoft.Agents.AI.Workflows;
using Moq;
using Moq.AutoMock;
using Monitoring.Application;
using Monitoring.Domain;

namespace Monitoring.UnitTests;

/// <summary>
/// Tests for <see cref="DecideAccessExecutor"/>, per the <c>access-decision-policy</c> and
/// <c>identity-verification</c> specs: a forwarded distance delegates to
/// <see cref="IAccessDecisionPolicy"/>; an explicit no-match (<see cref="MonitoringWorkflowContext.Distance"/>
/// is <c>null</c>) resolves <see cref="AccessOutcome.Denied"/> directly, without the policy ever
/// being invoked — proven via <c>Times.Never</c>, not merely by inspecting the resulting outcome.
/// </summary>
public sealed class DecideAccessExecutorTests
{
    private static MonitoringWorkflowContext CreateInput(double? distance) =>
        new("image.png", Stream.Null, EmbeddingVector: [0.1f, 0.2f, 0.3f], CandidateMemberId: null, Distance: distance, Decision: null);

    [Fact]
    public async Task Distance_present_delegates_to_the_access_decision_policy()
    {
        var mocker = new AutoMocker();
        mocker.Use(new ActivitySource("Test"));
        mocker.GetMock<IAccessDecisionPolicy>()
            .Setup(x => x.Decide(0.2))
            .Returns(new AccessDecisionResult(AccessOutcome.Allowed, 0.2, "Within allowed distance"));
        var executor = mocker.CreateInstance<DecideAccessExecutor>();

        MonitoringWorkflowContext result = await executor.HandleAsync(CreateInput(0.2), Mock.Of<IWorkflowContext>(), CancellationToken.None);

        Assert.NotNull(result.Decision);
        Assert.Equal(AccessOutcome.Allowed, result.Decision!.Outcome);
        mocker.GetMock<IAccessDecisionPolicy>().Verify(x => x.Decide(0.2), Times.Once);
    }

    [Fact]
    public async Task No_match_resolves_Denied_directly_without_calling_the_policy()
    {
        var mocker = new AutoMocker();
        mocker.Use(new ActivitySource("Test"));
        var executor = mocker.CreateInstance<DecideAccessExecutor>();

        MonitoringWorkflowContext result = await executor.HandleAsync(CreateInput(null), Mock.Of<IWorkflowContext>(), CancellationToken.None);

        Assert.NotNull(result.Decision);
        Assert.Equal(AccessOutcome.Denied, result.Decision!.Outcome);
        Assert.Null(result.Decision.Distance);
        mocker.GetMock<IAccessDecisionPolicy>()
            .Verify(x => x.Decide(It.IsAny<double>()), Times.Never);
    }
}
