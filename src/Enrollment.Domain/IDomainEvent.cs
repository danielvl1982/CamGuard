namespace Enrollment.Domain;

/// <summary>
/// Marker interface for domain events raised by <see cref="EntityBase{TId}"/>-derived
/// aggregates. A later Monitoring/Notification integration translates these into
/// integration events (per <c>dotnet-modular-monolith</c>); this module only raises them.
/// </summary>
public interface IDomainEvent;
