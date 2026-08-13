using MX.Application.Notifications;
using MX.Domain.Tickets;

namespace MX.Application.Tests.Notifications;

/// <summary>
/// The wording and, more importantly, the tracking link — the one piece of these
/// emails the README names explicitly and the one a customer will actually click.
/// </summary>
public class TicketEmailComposerTests
{
    private static readonly NotificationSettings Settings = new("https://support.example.com");

    private static TicketEmailComposer Composer() => new(Settings);

    private static Ticket AnyTicket(string? summary = null) =>
        Ticket.Create("Ada Lovelace", "ada@example.com", "The printer is on fire.", summary);

    [Fact]
    public void Confirmation_is_addressed_to_the_customer()
    {
        var ticket = AnyTicket();

        var message = Composer().Confirmation(ticket);

        Assert.Equal("ada@example.com", message.To);
        Assert.Contains("Ada Lovelace", message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Confirmation_carries_a_tracking_link_to_the_ticket()
    {
        var ticket = AnyTicket();

        var message = Composer().Confirmation(ticket);

        Assert.Contains($"https://support.example.com/tickets/{ticket.Id}", message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Tracking_link_does_not_double_up_slashes()
    {
        var composer = new TicketEmailComposer(new NotificationSettings("https://support.example.com/"));
        var ticket = AnyTicket();

        var message = composer.Confirmation(ticket);

        Assert.DoesNotContain("com//tickets", message.Body, StringComparison.Ordinal);
        Assert.Contains($"com/tickets/{ticket.Id}", message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Confirmation_repeats_what_the_customer_reported()
    {
        var message = Composer().Confirmation(AnyTicket());

        Assert.Contains("The printer is on fire.", message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Confirmation_includes_the_summary_when_there_is_one()
    {
        var message = Composer().Confirmation(AnyTicket(summary: "Printer ablaze."));

        Assert.Contains("Printer ablaze.", message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Confirmation_omits_the_summary_line_entirely_when_there_is_none()
    {
        // An "In short:" heading with nothing after it would read as a bug.
        var message = Composer().Confirmation(AnyTicket(summary: null));

        Assert.DoesNotContain("In short:", message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Status_change_names_both_the_old_and_new_status()
    {
        var ticket = AnyTicket();

        var message = Composer().StatusChanged(ticket, TicketStatus.New, TicketStatus.InProgress);

        Assert.Contains("New", message.Body, StringComparison.Ordinal);
        Assert.Contains("In Progress", message.Body, StringComparison.Ordinal);
        Assert.Contains("In Progress", message.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public void Status_change_uses_the_display_spelling_not_the_enum_name()
    {
        var message = Composer().StatusChanged(AnyTicket(), TicketStatus.New, TicketStatus.InProgress);

        Assert.DoesNotContain("InProgress", message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolution_change_quotes_the_new_text()
    {
        var ticket = AnyTicket();
        ticket.SetResolution("We replaced the fuser unit.");

        var message = Composer().ResolutionChanged(ticket);

        Assert.Contains("We replaced the fuser unit.", message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolution_cleared_still_says_something_sensible()
    {
        var ticket = AnyTicket();

        var message = Composer().ResolutionChanged(ticket);

        Assert.Contains("cleared", message.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("An update from our team:", message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_message_identifies_the_ticket()
    {
        var ticket = AnyTicket();
        var composer = Composer();

        foreach (var message in new[]
                 {
                     composer.Confirmation(ticket),
                     composer.StatusChanged(ticket, TicketStatus.New, TicketStatus.Closed),
                     composer.ResolutionChanged(ticket)
                 })
        {
            Assert.Contains(ticket.Id.ToString(), message.Body, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(message.Subject));
            Assert.Equal(ticket.Email, message.To);
        }
    }
}
