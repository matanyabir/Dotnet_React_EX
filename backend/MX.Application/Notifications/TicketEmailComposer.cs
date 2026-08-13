using MX.Application.Abstractions;
using MX.Domain.Tickets;

namespace MX.Application.Notifications;

/// <summary>
/// Writes the three customer emails.
///
/// Separated from the handlers so the wording can be tested without a sender, and
/// so all three messages sit side by side where their tone can be kept consistent.
/// </summary>
public sealed class TicketEmailComposer(NotificationSettings settings)
{
    public EmailMessage Confirmation(Ticket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var body =
            $"""
             Hello {ticket.Name},

             Thanks for getting in touch. We have logged your issue and someone will
             look at it shortly.

             Reference: {ticket.Id}
             Track your ticket: {settings.TrackingLinkFor(ticket.Id)}

             What you told us:
             {ticket.Description}
             {SummaryLine(ticket)}
             — MX Support
             """;

        return new EmailMessage(ticket.Email, $"We have received your ticket ({Short(ticket.Id)})", body);
    }

    public EmailMessage StatusChanged(Ticket ticket, TicketStatus from, TicketStatus to)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var body =
            $"""
             Hello {ticket.Name},

             The status of your ticket has changed from {from.ToDisplayName()} to {to.ToDisplayName()}.

             Reference: {ticket.Id}
             Track your ticket: {settings.TrackingLinkFor(ticket.Id)}

             — MX Support
             """;

        return new EmailMessage(
            ticket.Email,
            $"Your ticket is now {to.ToDisplayName()} ({Short(ticket.Id)})",
            body);
    }

    public EmailMessage ResolutionChanged(Ticket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        // Clearing the text is still worth telling the customer about — the note
        // they were reading yesterday is gone, and silence would be stranger.
        var update = string.IsNullOrWhiteSpace(ticket.Resolution)
            ? "The notes on your ticket have been cleared while we take another look."
            : $"An update from our team:{Environment.NewLine}{Environment.NewLine}{ticket.Resolution}";

        var body =
            $"""
             Hello {ticket.Name},

             {update}

             Reference: {ticket.Id}
             Track your ticket: {settings.TrackingLinkFor(ticket.Id)}

             — MX Support
             """;

        return new EmailMessage(ticket.Email, $"An update on your ticket ({Short(ticket.Id)})", body);
    }

    private static string SummaryLine(Ticket ticket) =>
        string.IsNullOrWhiteSpace(ticket.Summary)
            ? string.Empty
            : $"{Environment.NewLine}In short: {ticket.Summary}{Environment.NewLine}";

    /// <summary>First segment of the id — enough to recognise a subject line by.</summary>
    private static string Short(Guid id) => id.ToString()[..8];
}
