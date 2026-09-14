using Monitoring.Domain;

namespace Monitoring.Application;

/// <summary>
/// Port for firing an <see cref="AccessAlert"/> for a finalized access-decision outcome, per the
/// <c>access-notification</c> spec. The only implementation in this change is
/// <c>LoggingNotificationService</c> (Infrastructure) — a logging placeholder, not a real
/// delivery channel. A thrown exception from this port MUST be caught and logged by the caller
/// (<c>NotifyExecutor</c>), never left to crash the workflow run.
/// </summary>
public interface INotificationService
{
    Task NotifyAsync(AccessAlert alert, CancellationToken cancellationToken);
}

/// <summary>
/// Channel-agnostic alert DTO fired for every access-decision outcome (including
/// <see cref="AccessOutcome.Allowed"/>). Carries a reference to the archived image, never raw
/// image bytes, per the <c>access-notification</c> spec's "Alert Content Shape" requirement.
/// </summary>
public sealed record AccessAlert(
    Guid? MemberId,
    double? Distance,
    AccessOutcome Outcome,
    string ImageReference,
    DateTimeOffset OccurredAtUtc,
    string? Reason);
