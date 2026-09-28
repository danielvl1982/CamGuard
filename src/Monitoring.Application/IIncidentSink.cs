using Monitoring.Domain;

namespace Monitoring.Application;

/// <summary>
/// Terminal incident signal forwarded by <see cref="NotifyExecutor"/> after every
/// finalized outcome (incident-log T3). Primitives only — Application must never
/// reference <c>Enrollment.Domain</c> (see Monitoring.Application.csproj).
/// The adapter (<c>Monitoring.Infrastructure.IncidentSinkAdapter</c>) maps this to
/// <c>Enrollment.Domain.Incident</c> + <c>IIncidentRepository</c>.
/// </summary>
/// <param name="ImageReference">Path/reference to the ingested file, for audit/snapshot.</param>
/// <param name="CandidateMemberId">Matched member, when the verify stage found one.</param>
/// <param name="Distance">Forwarded cosine distance, null on explicit no-match.</param>
/// <param name="Outcome">Final access outcome for this run.</param>
/// <param name="Reason">Decision reason, persisted as the incident note.</param>
/// <param name="OccurredAtUtc">Run timestamp (from injected TimeProvider).</param>
public sealed record IncidentSignal(
    string ImageReference,
    Guid? CandidateMemberId,
    double? Distance,
    AccessOutcome Outcome,
    string? Reason,
    DateTimeOffset OccurredAtUtc);

/// <summary>
/// Port for automatic incident capture (incident-log T3). Implementations must
/// never throw out of the workflow — <see cref="NotifyExecutor"/> already
/// isolates sink failures — but should still avoid throwing for ignored
/// outcomes (Allowed, gray-zone HumanVerificationRequired).
/// </summary>
public interface IIncidentSink
{
    Task RecordAsync(IncidentSignal signal, CancellationToken cancellationToken);
}
