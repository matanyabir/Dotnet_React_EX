using MX.Domain.Common;
using MX.Domain.Tickets.Events;

namespace MX.Domain.Tickets;

/// <summary>
/// A customer support ticket.
///
/// Every property has a private setter and every change goes through a method
/// that decides whether the change is real, stamps <see cref="UpdatedAt"/>, and
/// raises the matching domain event. That is what makes "email on status change"
/// a property of the model rather than something a caller must remember to do.
/// </summary>
public sealed class Ticket : Entity
{
    private Ticket(
        Guid id,
        string name,
        string email,
        string description,
        string? summary,
        string? imageUrl,
        TicketStatus status,
        string? resolution,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Name = name;
        Email = email;
        Description = description;
        Summary = summary;
        ImageUrl = imageUrl;
        Status = status;
        Resolution = resolution;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }
    public string Name { get; private set; }
    public string Email { get; private set; }
    public string Description { get; private set; }

    /// <summary>AI-generated précis of <see cref="Description"/>. Null when unavailable.</summary>
    public string? Summary { get; private set; }

    /// <summary>Storage-relative path such as <c>uploads/laptop.jpg</c>. Null when no image.</summary>
    public string? ImageUrl { get; private set; }

    public TicketStatus Status { get; private set; }

    /// <summary>What the support agent did about it. Null until an agent fills it in.</summary>
    public string? Resolution { get; private set; }

    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Files a brand new ticket. Raises <see cref="TicketCreated"/>.
    /// </summary>
    public static Ticket Create(
        string name,
        string email,
        string description,
        string? summary = null,
        string? imageUrl = null,
        TimeProvider? timeProvider = null)
    {
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();

        var ticket = new Ticket(
            id: Guid.NewGuid(),
            name: Require(name, nameof(name)),
            email: Require(email, nameof(email)),
            description: Require(description, nameof(description)),
            summary: Normalise(summary),
            imageUrl: Normalise(imageUrl),
            status: TicketStatus.New,
            resolution: null,
            createdAt: now,
            updatedAt: now);

        ticket.Raise(new TicketCreated(ticket));
        return ticket;
    }

    /// <summary>
    /// Rebuilds a ticket from storage. Deliberately raises no events: loading a
    /// row is not a business change, and treating it as one would re-send every
    /// notification on every startup.
    /// </summary>
    public static Ticket Restore(
        Guid id,
        string name,
        string email,
        string description,
        string? summary,
        string? imageUrl,
        TicketStatus status,
        string? resolution,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) =>
        new(id,
            Require(name, nameof(name)),
            Require(email, nameof(email)),
            Require(description, nameof(description)),
            Normalise(summary),
            Normalise(imageUrl),
            status,
            Normalise(resolution),
            createdAt,
            updatedAt);

    /// <summary>
    /// Moves the ticket to <paramref name="newStatus"/>.
    /// </summary>
    /// <returns><c>true</c> if the status actually changed; <c>false</c> for a no-op.</returns>
    public bool ChangeStatus(TicketStatus newStatus, TimeProvider? timeProvider = null)
    {
        if (newStatus == Status)
        {
            return false;
        }

        var previous = Status;
        Status = newStatus;
        Touch(timeProvider);
        Raise(new TicketStatusChanged(this, previous, newStatus));
        return true;
    }

    /// <summary>
    /// Sets the agent's resolution text. Blank and null mean the same thing —
    /// "not resolved yet" — so switching between them is not a change.
    /// </summary>
    /// <returns><c>true</c> if the text actually changed; <c>false</c> for a no-op.</returns>
    public bool SetResolution(string? resolution, TimeProvider? timeProvider = null)
    {
        var normalised = Normalise(resolution);
        if (string.Equals(normalised, Resolution, StringComparison.Ordinal))
        {
            return false;
        }

        var previous = Resolution;
        Resolution = normalised;
        Touch(timeProvider);
        Raise(new TicketResolutionChanged(this, previous, normalised));
        return true;
    }

    /// <summary>
    /// Attaches or replaces the AI summary. Silent by design: a summary is a
    /// derived convenience, not a change the customer needs telling about.
    /// </summary>
    public void SetSummary(string? summary, TimeProvider? timeProvider = null)
    {
        var normalised = Normalise(summary);
        if (string.Equals(normalised, Summary, StringComparison.Ordinal))
        {
            return;
        }

        Summary = normalised;
        Touch(timeProvider);
    }

    /// <summary>Attaches or replaces the uploaded image path.</summary>
    public void AttachImage(string? imageUrl, TimeProvider? timeProvider = null)
    {
        var normalised = Normalise(imageUrl);
        if (string.Equals(normalised, ImageUrl, StringComparison.Ordinal))
        {
            return;
        }

        ImageUrl = normalised;
        Touch(timeProvider);
    }

    private void Touch(TimeProvider? timeProvider) =>
        UpdatedAt = (timeProvider ?? TimeProvider.System).GetUtcNow();

    /// <summary>Blank optional text is stored as null so "" and null cannot both mean empty.</summary>
    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Require(string value, string paramName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", paramName)
            : value.Trim();
}
