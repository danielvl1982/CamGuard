namespace Enrollment.Domain;

/// <summary>
/// Read-model for one recorded incident (incident-log T2, OI2 field set): an access
/// event that matters (unknown face, denied entry, annulled-member attempt) or a
/// manual operator note. Written by the T3 capture hook / T4 manual form and listed
/// newest-first. Incidents are evidence, never access: they carry no annul semantics.
/// </summary>
public sealed class Incident : EntityBase<IncidentId>
{
    private Incident(
        IncidentId id,
        DateTime occurredAtUtc,
        IncidentKind kind,
        MemberId? memberId,
        string? note,
        string? snapshotImagePath)
    {
        Id = id;
        OccurredAtUtc = occurredAtUtc;
        Kind = kind;
        MemberId = memberId;
        Note = note;
        SnapshotImagePath = snapshotImagePath;
    }

    /// <summary>When the incident happened, always UTC.</summary>
    public DateTime OccurredAtUtc { get; }

    public IncidentKind Kind { get; }

    /// <summary>Set only when a known member is involved; null for unknown faces and pure notes.</summary>
    public MemberId? MemberId { get; }

    public string? Note { get; }

    public string? SnapshotImagePath { get; }

    /// <summary>
    /// Records a new incident, normalizing the timestamp to UTC and blank
    /// optionals to null. Returns the instance directly (no <c>Result</c>): every
    /// field is either type-constrained (<paramref name="kind"/>, the timestamp)
    /// or optional, so no domain-invariant violation is expressible — a null
    /// <paramref name="id"/> is a programming error and throws.
    /// </summary>
    public static Incident Create(
        IncidentId id,
        DateTime occurredAtUtc,
        IncidentKind kind,
        MemberId? memberId = null,
        string? note = null,
        string? snapshotImagePath = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        return new Incident(
            id,
            NormalizeTimestamp(occurredAtUtc),
            kind,
            memberId,
            NormalizeOptional(note),
            NormalizeOptional(snapshotImagePath));
    }

    /// <summary>Reconstitutes an incident from persistence without raising domain events.</summary>
    public static Incident Rehydrate(
        IncidentId id,
        DateTime occurredAtUtc,
        IncidentKind kind,
        MemberId? memberId = null,
        string? note = null,
        string? snapshotImagePath = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        return new Incident(
            id,
            NormalizeTimestamp(occurredAtUtc),
            kind,
            memberId,
            NormalizeOptional(note),
            NormalizeOptional(snapshotImagePath));
    }

    private static DateTime NormalizeTimestamp(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
