namespace Monitoring.Domain;

/// <summary>
/// Lifecycle status of a <see cref="PendingVerification"/> record. Per the
/// <c>pending-verification-record</c> spec's "No Automatic Resolution" requirement, this change
/// never transitions a record out of <see cref="Pending"/> — a future UI/API write path (out of
/// scope here) would add resolved statuses.
/// </summary>
public enum PendingVerificationStatus
{
    Pending
}
