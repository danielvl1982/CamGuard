using System.Diagnostics;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using Monitoring.Application;
using Monitoring.Domain;

namespace Monitoring.UnitTests;

/// <summary>
/// Tests for <see cref="NotifyExecutor"/>, per the <c>access-notification</c> and
/// <c>pending-verification-record</c> specs: an alert fires for every outcome including
/// <see cref="AccessOutcome.Allowed"/>, the alert never carries raw image bytes, and a thrown
/// <see cref="INotificationService.NotifyAsync"/> is isolated so the run still completes.
/// </summary>
public sealed class NotifyExecutorTests
{
    private static MonitoringWorkflowContext CreateInput(AccessDecisionResult decision, Guid? candidateMemberId = null) =>
        new("processed/image.png", Stream.Null, EmbeddingVector: null, candidateMemberId, decision.Distance, decision);

    private static AutoMocker CreateMocker()
    {
        var mocker = new AutoMocker();
        mocker.Use(new ActivitySource("Test"));
        mocker.Use(TimeProvider.System);
        return mocker;
    }

    public static IEnumerable<object[]> AllOutcomes()
    {
        yield return [AccessOutcome.Allowed];
        yield return [AccessOutcome.Denied];
        yield return [AccessOutcome.HumanVerificationRequired];
    }

    [Theory]
    [MemberData(nameof(AllOutcomes))]
    public async Task Fires_a_notification_for_every_outcome_including_Allowed(AccessOutcome outcome)
    {
        var mocker = CreateMocker();
        var decision = new AccessDecisionResult(outcome, Distance: 0.4, Reason: "test");
        var executor = mocker.CreateInstance<NotifyExecutor>();

        await executor.HandleAsync(CreateInput(decision), Mock.Of<IWorkflowContext>(), CancellationToken.None);

        mocker.GetMock<INotificationService>()
            .Verify(x => x.NotifyAsync(It.Is<AccessAlert>(a => a.Outcome == outcome), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Alert_carries_an_image_reference_never_raw_image_bytes()
    {
        var mocker = CreateMocker();
        var decision = new AccessDecisionResult(AccessOutcome.Allowed, Distance: 0.1, Reason: "close match");
        var executor = mocker.CreateInstance<NotifyExecutor>();

        await executor.HandleAsync(CreateInput(decision), Mock.Of<IWorkflowContext>(), CancellationToken.None);

        // AccessAlert's shape (a string ImageReference, no byte[]/Stream field) already makes raw
        // image-byte transport a compile-time impossibility; this asserts the reference is correct.
        mocker.GetMock<INotificationService>()
            .Verify(
                x => x.NotifyAsync(It.Is<AccessAlert>(a => a.ImageReference == "processed/image.png"), It.IsAny<CancellationToken>()),
                Times.Once);
    }

    [Fact]
    public async Task Notification_failure_is_caught_logged_and_the_run_still_completes()
    {
        var mocker = CreateMocker();
        mocker.GetMock<INotificationService>()
            .Setup(x => x.NotifyAsync(It.IsAny<AccessAlert>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated notification failure"));
        var decision = new AccessDecisionResult(AccessOutcome.Denied, Distance: 0.9, Reason: "far match");
        var executor = mocker.CreateInstance<NotifyExecutor>();

        var completed = await executor.HandleAsync(CreateInput(decision), Mock.Of<IWorkflowContext>(), CancellationToken.None);

        Assert.True(completed);
        mocker.GetMock<ILogger<NotifyExecutor>>().Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HumanVerificationRequired_persists_a_pending_verification_record()
    {
        var mocker = CreateMocker();
        var decision = new AccessDecisionResult(AccessOutcome.HumanVerificationRequired, Distance: 0.45, Reason: "mid-threshold");
        var executor = mocker.CreateInstance<NotifyExecutor>();

        await executor.HandleAsync(CreateInput(decision), Mock.Of<IWorkflowContext>(), CancellationToken.None);

        mocker.GetMock<IPendingVerificationRepository>()
            .Verify(
                x => x.SaveAsync(It.Is<PendingVerification>(p => p.Status == PendingVerificationStatus.Pending), It.IsAny<CancellationToken>()),
                Times.Once);
    }

    [Fact]
    public async Task Allowed_outcome_does_not_persist_a_pending_verification_record()
    {
        var mocker = CreateMocker();
        var decision = new AccessDecisionResult(AccessOutcome.Allowed, Distance: 0.1, Reason: "close match");
        var executor = mocker.CreateInstance<NotifyExecutor>();

        await executor.HandleAsync(CreateInput(decision), Mock.Of<IWorkflowContext>(), CancellationToken.None);

        mocker.GetMock<IPendingVerificationRepository>()
            .Verify(x => x.SaveAsync(It.IsAny<PendingVerification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Denied_outcome_forwards_an_incident_signal_to_the_sink()
    {
        var mocker = CreateMocker();
        var memberId = Guid.NewGuid();
        var decision = new AccessDecisionResult(AccessOutcome.Denied, Distance: 0.9, Reason: "far match");
        var executor = mocker.CreateInstance<NotifyExecutor>();

        await executor.HandleAsync(CreateInput(decision, memberId), Mock.Of<IWorkflowContext>(), CancellationToken.None);

        mocker.GetMock<IIncidentSink>()
            .Verify(
                x => x.RecordAsync(
                    It.Is<IncidentSignal>(s =>
                        s.Outcome == AccessOutcome.Denied &&
                        s.CandidateMemberId == memberId &&
                        s.ImageReference == "processed/image.png"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
    }

    [Fact]
    public async Task Incident_sink_failure_is_caught_logged_and_the_run_still_completes()
    {
        var mocker = CreateMocker();
        mocker.GetMock<IIncidentSink>()
            .Setup(x => x.RecordAsync(It.IsAny<IncidentSignal>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated sink failure"));
        var decision = new AccessDecisionResult(AccessOutcome.Denied, Distance: null, Reason: "No enrolled member match found");
        var executor = mocker.CreateInstance<NotifyExecutor>();

        var completed = await executor.HandleAsync(CreateInput(decision), Mock.Of<IWorkflowContext>(), CancellationToken.None);

        Assert.True(completed);
        mocker.GetMock<ILogger<NotifyExecutor>>().Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
