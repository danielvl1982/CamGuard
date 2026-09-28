using Enrollment.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Monitoring.Application;
using Monitoring.Domain;
using Monitoring.Infrastructure;
using Moq;

namespace Monitoring.UnitTests;

/// <summary>
/// Tests for <see cref="IncidentSinkAdapter"/> (incident-log T3): outcome → kind
/// mapping, member linkage, and ignored outcomes.
/// </summary>
public sealed class IncidentSinkAdapterTests
{
    private static IncidentSinkAdapter CreateAdapter(Mock<IIncidentRepository> incidentsMock) =>
        new(incidentsMock.Object, NullLogger<IncidentSinkAdapter>.Instance);

    private static IncidentSignal Signal(
        AccessOutcome outcome,
        double? distance,
        Guid? memberId,
        string? reason = "test reason") =>
        new("watch/frame.png", memberId, distance, outcome, reason, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Denied_without_distance_persists_UnknownFace_without_member()
    {
        var incidentsMock = new Mock<IIncidentRepository>();
        var adapter = CreateAdapter(incidentsMock);

        await adapter.RecordAsync(Signal(AccessOutcome.Denied, null, null), CancellationToken.None);

        incidentsMock.Verify(
            x => x.SaveAsync(
                It.Is<Incident>(i => i.Kind == IncidentKind.UnknownFace && i.MemberId == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Denied_with_distance_persists_AccessDenied_with_member()
    {
        var incidentsMock = new Mock<IIncidentRepository>();
        var adapter = CreateAdapter(incidentsMock);
        var memberId = Guid.NewGuid();

        await adapter.RecordAsync(Signal(AccessOutcome.Denied, 0.9, memberId), CancellationToken.None);

        incidentsMock.Verify(
            x => x.SaveAsync(
                It.Is<Incident>(i =>
                    i.Kind == IncidentKind.AccessDenied &&
                    i.MemberId != null &&
                    i.MemberId.Value == memberId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Zero_face_short_circuit_persists_UnknownFace()
    {
        var incidentsMock = new Mock<IIncidentRepository>();
        var adapter = CreateAdapter(incidentsMock);

        await adapter.RecordAsync(
            Signal(AccessOutcome.HumanVerificationRequired, null, null, "No face detected"),
            CancellationToken.None);

        incidentsMock.Verify(
            x => x.SaveAsync(
                It.Is<Incident>(i => i.Kind == IncidentKind.UnknownFace),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(AccessOutcome.Allowed)]
    [InlineData(AccessOutcome.HumanVerificationRequired)]
    public async Task Allowed_and_gray_zone_HVR_persist_nothing(AccessOutcome outcome)
    {
        var incidentsMock = new Mock<IIncidentRepository>();
        var adapter = CreateAdapter(incidentsMock);

        await adapter.RecordAsync(Signal(outcome, 0.45, Guid.NewGuid(), "mid-threshold"), CancellationToken.None);

        incidentsMock.Verify(
            x => x.SaveAsync(It.IsAny<Incident>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
