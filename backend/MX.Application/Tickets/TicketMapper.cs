using MX.Application.Tickets.Dtos;
using MX.Domain.Tickets;

namespace MX.Application.Tickets;

/// <summary>
/// Entity to DTO, written by hand.
///
/// A convention-based mapper would save these few lines at the cost of a runtime
/// surprise whenever a property is renamed. Explicit assignment means the compiler
/// catches that, and there is no reflection behaviour to explain.
/// </summary>
internal static class TicketMapper
{
    public static TicketDto ToDto(this Ticket ticket) => new(
        Id: ticket.Id,
        Name: ticket.Name,
        Email: ticket.Email,
        Description: ticket.Description,
        Summary: ticket.Summary,
        ImageUrl: ticket.ImageUrl,
        Status: ticket.Status.ToDisplayName(),
        Resolution: ticket.Resolution,
        CreatedAt: ticket.CreatedAt,
        UpdatedAt: ticket.UpdatedAt);
}
