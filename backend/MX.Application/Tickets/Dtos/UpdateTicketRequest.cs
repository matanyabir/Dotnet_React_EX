namespace MX.Application.Tickets.Dtos;

/// <summary>
/// What an admin changes on the detail screen. Both fields are optional and
/// <c>null</c> means "leave this alone", which is what makes the two independently
/// editable without the client having to echo back the value it is not touching.
///
/// The distinction matters for <see cref="Resolution"/>: <c>null</c> leaves the
/// existing text in place, while <c>""</c> is an explicit request to clear it.
/// </summary>
public sealed record UpdateTicketRequest(
    string? Status = null,
    string? Resolution = null);
