using CSharpFunctionalExtensions;

namespace Monitoring.Domain;

/// <summary>
/// Terminal completion record for a workflow run that resolved
/// <see cref="AccessOutcome.HumanVerificationRequired"/>, whether from person-detection's
/// zero-face path or access-decision-policy's mid-threshold path — per the
/// <c>pending-verification-record</c> spec. Not a suspended/resumable workflow: resolving a
/// record requires a separate, out-of-scope write path.
///
/// <para>
/// Extends <see cref="Entity{TId}"/> directly (never <c>Enrollment.Domain.EntityBase&lt;TId&gt;</c>,
/// which lives in the internal <c>Enrollment.Domain</c> project that <c>Monitoring.*</c> is never
/// allowed to reference). This entity raises no domain events, per the <c>entity-model</c> skill's
/// decision gate ("entity with no domain events or audit → <c>Entity&lt;TId&gt;</c> directly").
/// </para>
/// </summary>
public sealed class PendingVerification : Entity<PendingVerificationId>
{
    private PendingVerification(
        PendingVerificationId id,
        string imageReference,
        double? distance,
        Guid? candidateMemberId,
        DateTimeOffset occurredAtUtc)
    {
        Id = id;
        ImageReference = imageReference;
        Distance = distance;
        CandidateMemberId = candidateMemberId;
        OccurredAtUtc = occurredAtUtc;
    }

    /// <summary>Reference to the archived image (never raw image bytes).</summary>
    public string ImageReference { get; }

    /// <summary>Candidate cosine distance value, or <c>null</c> when triggered by the zero-face path.</summary>
    public double? Distance { get; }

    /// <summary>Candidate matched member, or <c>null</c> when no candidate match was found.</summary>
    public Guid? CandidateMemberId { get; }

    /// <summary>UTC time the triggering event occurred, per an injected <c>TimeProvider</c> — never <c>DateTime.UtcNow</c>.</summary>
    public DateTimeOffset OccurredAtUtc { get; }

    public PendingVerificationStatus Status { get; private set; } = PendingVerificationStatus.Pending;

    /// <summary>
    /// Creates a new <see cref="PendingVerification"/> record, always starting in
    /// <see cref="PendingVerificationStatus.Pending"/> status, per the
    /// <c>pending-verification-record</c> spec's "Initial Pending Status" requirement.
    /// </summary>
    public static PendingVerification Create(
        PendingVerificationId id,
        string imageReference,
        double? distance,
        Guid? candidateMemberId,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageReference);

        return new PendingVerification(id, imageReference, distance, candidateMemberId, occurredAtUtc);
    }
}
