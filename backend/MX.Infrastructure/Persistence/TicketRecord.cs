using System.Text.Json.Serialization;
using MX.Domain.Tickets;

namespace MX.Infrastructure.Persistence;

/// <summary>
/// The on-disk shape of a ticket — a persistence model, kept separate from the
/// <see cref="Ticket"/> entity on purpose.
///
/// The entity has private setters and invariants, which a deserializer cannot
/// satisfy; and the domain should not carry JSON attributes describing a storage
/// format it is not supposed to know about. Mapping between the two costs a few
/// lines and keeps both honest.
///
/// Property order mirrors the supplied dataset.json so a rewritten file stays
/// diff-friendly against the original.
/// </summary>
internal sealed class TicketRecord
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Empty string rather than null — the supplied dataset uses "" for absent text.</summary>
    public string Summary { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    [JsonConverter(typeof(TicketStatusJsonConverter))]
    public TicketStatus Status { get; set; }

    public string Resolution { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Rebuilds the entity. <see cref="Ticket.Restore"/> converts "" back to null,
    /// so the domain never sees two different spellings of "absent".
    /// </summary>
    public Ticket ToDomain() =>
        Ticket.Restore(
            id: Guid.Parse(Id),
            name: Name,
            email: Email,
            description: Description,
            summary: Summary,
            imageUrl: ImageUrl,
            status: Status,
            resolution: Resolution,
            createdAt: CreatedAt,
            updatedAt: UpdatedAt);

    public static TicketRecord FromDomain(Ticket ticket) => new()
    {
        Id = ticket.Id.ToString(),
        Name = ticket.Name,
        Email = ticket.Email,
        Description = ticket.Description,
        Summary = ticket.Summary ?? string.Empty,
        ImageUrl = ticket.ImageUrl ?? string.Empty,
        Status = ticket.Status,
        Resolution = ticket.Resolution ?? string.Empty,
        CreatedAt = ticket.CreatedAt,
        UpdatedAt = ticket.UpdatedAt
    };
}
