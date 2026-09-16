using System.Diagnostics;
using Enrollment.PublicApi;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Monitoring.Application;
using Monitoring.Domain;

namespace Monitoring.UnitTests;

/// <summary>
/// Tests for <see cref="DetectPersonExecutor"/>, per the <c>person-detection</c> spec: one or more
/// detected faces proceed toward identity verification; zero detected faces route to
/// <see cref="AccessOutcome.HumanVerificationRequired"/> and identity verification is never
/// attempted — proven both at the executor's own output and, for the last case, by running the
/// real workflow graph and verifying the mocked <see cref="IMemberDirectoryReader"/> was never
/// invoked (not merely that the outcome looks right).
/// </summary>
public sealed class DetectPersonExecutorTests
{
    private static MonitoringWorkflowContext CreateInput() =>
        new("image.png", Stream.Null, EmbeddingVector: null, CandidateMemberId: null, Distance: null, Decision: null);

    [Fact]
    public async Task Face_detected_proceeds_toward_identity_verification()
    {
        var mocker = new AutoMocker();
        mocker.Use(new ActivitySource("Test"));
        float[] embedding = [0.1f, 0.2f, 0.3f];
        mocker.GetMock<IPersonDetector>()
            .Setup(x => x.DetectAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PersonDetectionResult(true, embedding));
        var executor = mocker.CreateInstance<DetectPersonExecutor>();

        var result = await executor.HandleAsync(CreateInput(), Mock.Of<IWorkflowContext>(), CancellationToken.None);

        // Decision stays unset — this is exactly what routes the graph's normal edge to
        // VerifyIdentityExecutor rather than the zero-face short-circuit edge to NotifyExecutor.
        Assert.Null(result.Decision);
        Assert.Equal(embedding, result.EmbeddingVector);
    }

    [Fact]
    public async Task Zero_faces_detected_resolves_HumanVerificationRequired_without_reaching_verify()
    {
        var mocker = new AutoMocker();
        mocker.Use(new ActivitySource("Test"));
        mocker.GetMock<IPersonDetector>()
            .Setup(x => x.DetectAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PersonDetectionResult(false, null));
        var executor = mocker.CreateInstance<DetectPersonExecutor>();

        var result = await executor.HandleAsync(CreateInput(), Mock.Of<IWorkflowContext>(), CancellationToken.None);

        Assert.NotNull(result.Decision);
        Assert.Equal(AccessOutcome.HumanVerificationRequired, result.Decision!.Outcome);
        Assert.Null(result.Decision.Distance);
        Assert.Null(result.EmbeddingVector);
    }

    [Fact]
    public async Task Zero_faces_detected_never_invokes_identity_verification_in_the_full_workflow()
    {
        var personDetectorMock = new Mock<IPersonDetector>();
        personDetectorMock
            .Setup(x => x.DetectAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PersonDetectionResult(false, null));
        var memberDirectoryReaderMock = new Mock<IMemberDirectoryReader>();
        var accessDecisionPolicyMock = new Mock<IAccessDecisionPolicy>();
        var notificationServiceMock = new Mock<INotificationService>();
        var pendingVerificationRepositoryMock = new Mock<IPendingVerificationRepository>();
        var activitySource = new ActivitySource("Test.Workflow");

        var detect = new DetectPersonExecutor(personDetectorMock.Object, activitySource);
        var verify = new VerifyIdentityExecutor(memberDirectoryReaderMock.Object, activitySource);
        var decide = new DecideAccessExecutor(accessDecisionPolicyMock.Object, activitySource);
        var notify = new NotifyExecutor(
            notificationServiceMock.Object,
            pendingVerificationRepositoryMock.Object,
            TimeProvider.System,
            NullLogger<NotifyExecutor>.Instance,
            activitySource);
        var host = new MonitoringWorkflowHost(detect, verify, decide, notify);

        await using var run = await InProcessExecution.RunAsync(host.Workflow, CreateInput(), cancellationToken: CancellationToken.None);

        // The mock was never called — not merely "the outcome looks right" — proving the
        // conditional short-circuit edge genuinely bypasses VerifyIdentityExecutor/DecideAccessExecutor.
        memberDirectoryReaderMock.Verify(
            x => x.FindBestMatchAsync(It.IsAny<float[]>(), It.IsAny<CancellationToken>()), Times.Never);
        accessDecisionPolicyMock.Verify(x => x.Decide(It.IsAny<double>()), Times.Never);
        notificationServiceMock.Verify(
            x => x.NotifyAsync(It.Is<AccessAlert>(a => a.Outcome == AccessOutcome.HumanVerificationRequired), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
