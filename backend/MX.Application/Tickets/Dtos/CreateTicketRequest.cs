namespace MX.Application.Tickets.Dtos;

/// <summary>
/// What a customer submits in the New Ticket modal.
///
/// Notably absent: status, summary, and timestamps. Those are the system's to
/// decide, and accepting them from the wire would let a caller file a ticket that
/// is already "Closed". <see cref="ImageUrl"/> is filled in by the API after the
/// uploaded file has been stored, not sent by the browser.
/// </summary>
public sealed record CreateTicketRequest(
    string Name,
    string Email,
    string Description,
    string? ImageUrl = null);
