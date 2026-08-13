using MX.Domain.Common;

namespace MX.Domain.Tickets.Events;

/// <summary>
/// Raised when a customer files a new ticket. The README requires a confirmation
/// email carrying a tracking link at this point.
/// </summary>
public sealed record TicketCreated(Ticket Ticket) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Raised only when the status actually changes value — re-saving the same
/// status is a no-op and must not notify the customer.
/// </summary>
public sealed record TicketStatusChanged(Ticket Ticket, TicketStatus From, TicketStatus To) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Raised only when the resolution text actually changes value.
/// </summary>
public sealed record TicketResolutionChanged(Ticket Ticket, string? From, string? To) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
