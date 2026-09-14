using Monitoring.Domain;

namespace Monitoring.Application;

/// <summary>
/// Port for persisting a terminal <see cref="PendingVerification"/> record, per the
/// <c>pending-verification-record</c> spec. Exact persistence mechanism (in-memory vs.
/// file-backed) is an Infrastructure decision (design.md's Open Questions) — the only
/// implementation shipped in this change is <c>InMemoryPendingVerificationRepository</c>.
/// </summary>
public interface IPendingVerificationRepository
{
    Task SaveAsync(PendingVerification pendingVerification, CancellationToken cancellationToken);
}
