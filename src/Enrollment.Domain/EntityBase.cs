using CSharpFunctionalExtensions;

namespace Enrollment.Domain;

/// <summary>
/// Extends <see cref="Entity{TId}"/> (identity + equality, from
/// <c>CSharpFunctionalExtensions</c>) with domain-event collection, per the
/// <c>entity-model</c> skill's "entity with domain events" base class.
/// </summary>
public abstract class EntityBase<TId> : Entity<TId>
    where TId : IComparable<TId>
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
