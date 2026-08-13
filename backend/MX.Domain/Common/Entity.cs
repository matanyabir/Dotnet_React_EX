namespace MX.Domain.Common;

/// <summary>
/// Base class for entities that record domain events.
///
/// Events accumulate on the entity as it is mutated and are drained by the
/// application layer once the change is safely persisted. This is what keeps
/// side effects such as email out of the entity itself.
/// </summary>
public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Events raised since the entity was loaded or last drained.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>
    /// Returns the pending events and clears them, so a second dispatch cannot
    /// resend the same notifications.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DrainDomainEvents()
    {
        var drained = _domainEvents.ToArray();
        _domainEvents.Clear();
        return drained;
    }
}
