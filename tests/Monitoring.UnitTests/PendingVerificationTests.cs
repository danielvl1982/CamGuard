using System.Diagnostics;
using Microsoft.Agents.AI.Workflows;
using Moq;
using Moq.AutoMock;
using Monitoring.Application;
using Monitoring.Domain;

namespace Monitoring.UnitTests;

/// <summary>Domain unit tests for <see cref="PendingVerification.Create"/> invariants.</summary>
public sealed class PendingVerificationTests
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_starts_in_Pending_status()
    {
        var record = PendingVerification.Create(
            PendingVerificationId.New(), "processed/image-1.png", distance: null, candidateMemberId: null, OccurredAt);

        Assert.Equal(PendingVerificationStatus.Pending, record.Status);
    }

    [Fact]
    public void Create_from_zero_face_path_allows_null_distance_and_null_candidate()
    {
        var record = PendingVerification.Create(
            PendingVerificationId.New(), "processed/image-2.png", distance: null, candidateMemberId: null, OccurredAt);

        Assert.Null(record.Distance);
        Assert.Null(record.CandidateMemberId);
        Assert.Equal("processed/image-2.png", record.ImageReference);
        Assert.Equal(OccurredAt, record.OccurredAtUtc);
    }

    [Fact]
    public void Create_from_mid_threshold_path_retains_distance_and_candidate()
    {
        var candidateMemberId = Guid.NewGuid();

        var record = PendingVerification.Create(
            PendingVerificationId.New(), "processed/image-3.png", distance: 0.55, candidateMemberId, OccurredAt);

        Assert.Equal(0.55, record.Distance);
        Assert.Equal(candidateMemberId, record.CandidateMemberId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_or_whitespace_image_reference(string imageReference)
    {
        Assert.Throws<ArgumentException>(() =>
            PendingVerification.Create(PendingVerificationId.New(), imageReference, null, null, OccurredAt));
    }

    [Fact]
    public void Create_throws_on_null_id()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PendingVerification.Create(null!, "processed/image-4.png", null, null, OccurredAt));
    }

    [Fact]
    public void Status_has_no_way_to_transition_out_of_Pending()
    {
        // pending-verification-record spec's "No Automatic Resolution" requirement:
        // PendingVerificationStatus defines exactly one value, so no code path anywhere in this
        // module can move a record out of Pending — resolving requires a future, out-of-scope
        // write path that would need to add a new status value first.
        PendingVerificationStatus[] statusValues = Enum.GetValues<PendingVerificationStatus>();

        Assert.Single(statusValues);
        Assert.Equal(PendingVerificationStatus.Pending, statusValues[0]);
    }
}

/// <summary>
/// Executor-level tests proving <see cref="NotifyExecutor"/> persists exactly one
/// <see cref="PendingVerification"/> record per <see cref="AccessOutcome.HumanVerificationRequired"/>
/// occurrence, from both triggering paths (zero-face and mid-threshold-distance), per the
/// <c>pending-verification-record</c> spec's "Terminal Record On HumanVerificationRequired" and
/// "Record Field Completeness" requirements.
/// </summary>
public sealed class PendingVerificationPersistenceTests
{
    private static AutoMocker CreateMocker()
    {
        var mocker = new AutoMocker();
        mocker.Use(new ActivitySource("Test"));
        mocker.Use(TimeProvider.System);
        return mocker;
    }

    [Fact]
    public async Task Zero_face_path_persists_exactly_one_record_with_null_distance_and_candidate()
    {
        AutoMocker mocker = CreateMocker();
        var decision = new AccessDecisionResult(AccessOutcome.HumanVerificationRequired, Distance: null, Reason: "No face detected");
        var input = new MonitoringWorkflowContext(
            "processed/zero-face.png", Stream.Null, EmbeddingVector: null, CandidateMemberId: null, Distance: null, decision);
        var executor = mocker.CreateInstance<NotifyExecutor>();

        await executor.HandleAsync(input, Mock.Of<IWorkflowContext>(), CancellationToken.None);

        mocker.GetMock<IPendingVerificationRepository>().Verify(
            x => x.SaveAsync(
                It.Is<PendingVerification>(p =>
                    p.Distance == null &&
                    p.CandidateMemberId == null &&
                    p.Status == PendingVerificationStatus.Pending &&
                    p.ImageReference == "processed/zero-face.png"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Mid_threshold_path_persists_exactly_one_record_with_distance_and_candidate()
    {
        AutoMocker mocker = CreateMocker();
        var candidateMemberId = Guid.NewGuid();
        var decision = new AccessDecisionResult(AccessOutcome.HumanVerificationRequired, Distance: 0.45, Reason: "Distance strictly between configured thresholds");
        var input = new MonitoringWorkflowContext(
            "processed/mid-threshold.png", Stream.Null, EmbeddingVector: [0.1f], candidateMemberId, Distance: 0.45, decision);
        var executor = mocker.CreateInstance<NotifyExecutor>();

        await executor.HandleAsync(input, Mock.Of<IWorkflowContext>(), CancellationToken.None);

        mocker.GetMock<IPendingVerificationRepository>().Verify(
            x => x.SaveAsync(
                It.Is<PendingVerification>(p =>
                    p.Distance == 0.45 &&
                    p.CandidateMemberId == candidateMemberId &&
                    p.Status == PendingVerificationStatus.Pending),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(AccessOutcome.Allowed)]
    [InlineData(AccessOutcome.Denied)]
    public async Task Non_HumanVerificationRequired_outcomes_never_persist_a_record(AccessOutcome outcome)
    {
        AutoMocker mocker = CreateMocker();
        var decision = new AccessDecisionResult(outcome, Distance: 0.2, Reason: "test");
        var input = new MonitoringWorkflowContext(
            "processed/other.png", Stream.Null, EmbeddingVector: null, CandidateMemberId: null, Distance: 0.2, decision);
        var executor = mocker.CreateInstance<NotifyExecutor>();

        await executor.HandleAsync(input, Mock.Of<IWorkflowContext>(), CancellationToken.None);

        mocker.GetMock<IPendingVerificationRepository>().Verify(
            x => x.SaveAsync(It.IsAny<PendingVerification>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
