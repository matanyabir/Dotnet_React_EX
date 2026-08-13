using MX.Domain.Common;

namespace MX.Application.Abstractions;

/// <summary>
/// Reacts to one kind of domain event.
///
/// Handlers are the "observers" in this design: <c>TicketService</c> announces
/// what happened and each handler decides independently what to do about it.
/// Stage 6 adds the email handlers; an audit log or webhook would be another
/// class here and no change to the service at all.
/// </summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}
