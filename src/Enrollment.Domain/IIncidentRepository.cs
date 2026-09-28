namespace Enrollment.Domain;

/// <summary>
/// Port for persisting incident-log records (incident-log T2, OI2 field set). The
/// Infrastructure adapter stores them in a plain SQLite table (no vector search —
/// incidents are only listed newest-first). Implementations upsert on save.
/// </summary>
public interface IIncidentRepository
{
    /// <summary>
    /// Records an incident, overwriting any existing record with the same id
    /// (re-recording is idempotent, never a duplicate row).
    /// </summary>
    Task SaveAsync(Incident incident, CancellationToken cancellationToken);

    /// <summary>
    /// Returns up to <paramref name="limit"/> incidents, newest-first
    /// (<see cref="Incident.OccurredAtUtc"/> descending, insertion order breaking
    /// ties). A <paramref name="limit"/> of zero or less returns an empty list.
    /// </summary>
    Task<IReadOnlyList<Incident>> ListRecentAsync(int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Point lookup by id for T4 member/incident links. Returns null when no
    /// incident with <paramref name="id"/> exists.
    /// </summary>
    Task<Incident?> FindByIdAsync(IncidentId id, CancellationToken cancellationToken);
}
