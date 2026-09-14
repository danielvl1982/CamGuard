namespace Monitoring.Domain;

/// <summary>
/// Strongly typed identity for <see cref="PendingVerification"/>. Mirrors
/// <c>Enrollment.Domain.MemberId</c>'s exact shape (<c>sealed record : IComparable&lt;TSelf&gt;</c>,
/// private constructor, <see cref="New"/> factory) as an independent, local value type owned
/// entirely by <c>Monitoring.Domain</c> — this type is never shared with or imported from
/// <c>Enrollment.Domain</c>, per this change's module-boundary rule (design.md).
/// </summary>
public sealed record PendingVerificationId : IComparable<PendingVerificationId>
{
    private PendingVerificationId(Guid value) => Value = value;

    public Guid Value { get; }

    public int CompareTo(PendingVerificationId? other) => other is null ? 1 : Value.CompareTo(other.Value);

    /// <summary>Generates a new, always-valid identity for a pending verification being recorded.</summary>
    public static PendingVerificationId New() => new(Guid.NewGuid());

    /// <summary>Constructs a <see cref="PendingVerificationId"/> from an existing value, rejecting <see cref="Guid.Empty"/>.</summary>
    public static PendingVerificationId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("PendingVerificationId cannot be Guid.Empty.", nameof(value));

        return new PendingVerificationId(value);
    }

    public override string ToString() => Value.ToString();
}
