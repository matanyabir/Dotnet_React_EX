using MX.Domain.Common;

namespace MX.Application.Abstractions;

/// <summary>
/// Hands domain events to whoever is listening.
///
/// This is the seam that keeps <c>TicketService</c> ignorant of email: the service
/// records what happened, the dispatcher decides who cares. Adding a second
/// reaction later (an audit log, a webhook) touches no service code.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Called only after the change is safely persisted, so a failed save can
    /// never produce a notification about something that did not happen.
    /// </summary>
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
