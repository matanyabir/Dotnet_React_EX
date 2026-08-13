namespace MX.Application.Tickets.Dtos;

/// <summary>
/// A ticket as the API presents it.
///
/// <see cref="Status"/> is the display string ("In Progress"), not the enum name,
/// so the frontend and the JSON store speak the same vocabulary. Absent text is
/// <c>null</c> rather than "", letting the UI test one thing to decide whether to
/// render a field.
/// </summary>
public sealed record TicketDto(
    Guid Id,
    string Name,
    string Email,
    string Description,
    string? Summary,
    string? ImageUrl,
    string Status,
    string? Resolution,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
