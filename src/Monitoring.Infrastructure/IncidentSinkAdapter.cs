using Enrollment.Domain;
using Microsoft.Extensions.Logging;
using Monitoring.Application;
using Monitoring.Domain;

namespace Monitoring.Infrastructure;

/// <summary>
/// Automatic incident capture adapter (incident-log T3): maps a terminal
/// <see cref="IncidentSignal"/> to an <see cref="Incident"/> persisted via
/// <see cref="IIncidentRepository"/>. Lives here (not Application) so the
/// workflow layer never references <c>Enrollment.Domain</c>.
/// Mapping (v1):
/// - Denied + Distance null (explicit no-match) → UnknownFace, no member.
/// - Denied + Distance set (policy denied a detected face) → AccessDenied, with member when known.
/// - HumanVerificationRequired + "No face detected" (zero-face short-circuit) → UnknownFace.
/// - Allowed + any other HVR (gray zone) → ignored, no incident.
/// AnnulledAttempt is reserved: annulled members are filtered inside
/// FindNearestAsync, so an annulled attempt currently surfaces as UnknownFace.
/// </summary>
public sealed class IncidentSinkAdapter(
    IIncidentRepository incidentRepository,
    ILogger<IncidentSinkAdapter> logger) : IIncidentSink
{
    private readonly IIncidentRepository _incidentRepository =
        incidentRepository ?? throw new ArgumentNullException(nameof(incidentRepository));

    private readonly ILogger<IncidentSinkAdapter> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task RecordAsync(IncidentSignal signal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signal);

        var kind = MapKind(signal);
        if (kind is null)
        {
            return;
        }

        MemberId? memberId = null;
        if (kind == IncidentKind.AccessDenied && signal.CandidateMemberId is { } candidate && candidate != Guid.Empty)
        {
            var memberIdResult = MemberId.From(candidate);
            if (memberIdResult.IsFailure)
            {
                _logger.LogWarning(
                    "Incident capture ignoring corrupt member id '{MemberId}' for {ImageReference}.",
                    candidate,
                    signal.ImageReference);
            }
            else
            {
                memberId = memberIdResult.Value;
            }
        }

        var incident = Incident.Create(
            IncidentId.New(),
            signal.OccurredAtUtc.UtcDateTime,
            kind.Value,
            memberId,
            signal.Reason,
            signal.ImageReference);

        await _incidentRepository.SaveAsync(incident, cancellationToken).ConfigureAwait(false);
    }

    private static IncidentKind? MapKind(IncidentSignal signal) => signal switch
    {
        { Outcome: AccessOutcome.Denied, Distance: null } => IncidentKind.UnknownFace,
        { Outcome: AccessOutcome.Denied } => IncidentKind.AccessDenied,
        { Outcome: AccessOutcome.HumanVerificationRequired, Reason: "No face detected" } => IncidentKind.UnknownFace,
        _ => null,
    };
}
