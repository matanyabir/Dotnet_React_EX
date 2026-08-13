namespace MX.Domain.Common;

/// <summary>
/// Something that happened to an aggregate which other parts of the system may
/// need to react to. Events are recorded by the entity and dispatched after the
/// change has been persisted, so a failed save never sends a notification.
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
