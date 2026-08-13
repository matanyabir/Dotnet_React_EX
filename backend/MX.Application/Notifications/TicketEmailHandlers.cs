using MX.Application.Abstractions;
using MX.Domain.Tickets.Events;

namespace MX.Application.Notifications;

/// <summary>
/// Emails the customer when their ticket is filed.
///
/// One class per event, which is what turns the README's three email cases into
/// three small units rather than three branches inside the ticket service. Each
/// can be read, tested, and removed independently.
/// </summary>
public sealed class SendConfirmationOnTicketCreated(
    IEmailSender emailSender,
    TicketEmailComposer composer) : IDomainEventHandler<TicketCreated>
{
    public Task HandleAsync(TicketCreated domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return emailSender.SendAsync(composer.Confirmation(domainEvent.Ticket), cancellationToken);
    }
}

/// <summary>
/// Emails the customer when the status moves.
///
/// The event is only raised for a real change, so this handler needs no guard of
/// its own — "do not email twice for the same save" is settled in the entity.
/// </summary>
public sealed class SendNoticeOnStatusChanged(
    IEmailSender emailSender,
    TicketEmailComposer composer) : IDomainEventHandler<TicketStatusChanged>
{
    public Task HandleAsync(TicketStatusChanged domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var message = composer.StatusChanged(domainEvent.Ticket, domainEvent.From, domainEvent.To);

        return emailSender.SendAsync(message, cancellationToken);
    }
}

/// <summary>Emails the customer when the resolution text changes.</summary>
public sealed class SendNoticeOnResolutionChanged(
    IEmailSender emailSender,
    TicketEmailComposer composer) : IDomainEventHandler<TicketResolutionChanged>
{
    public Task HandleAsync(TicketResolutionChanged domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return emailSender.SendAsync(composer.ResolutionChanged(domainEvent.Ticket), cancellationToken);
    }
}
