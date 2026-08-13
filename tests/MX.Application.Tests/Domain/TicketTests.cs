using MX.Domain.Tickets;
using MX.Domain.Tickets.Events;

namespace MX.Application.Tests.Domain;

/// <summary>
/// The entity's rules about what counts as a change. The email requirement rests
/// entirely on these: a notification is sent for each event raised, so an event
/// raised on a no-op would spam the customer.
/// </summary>
public class TicketTests
{
    private static Ticket AnyTicket() =>
        Ticket.Create("Grace Hopper", "grace@example.com", "The compiler has a moth in it.");

    [Fact]
    public void Create_starts_the_ticket_as_new_and_unresolved()
    {
        var ticket = AnyTicket();

        Assert.Equal(TicketStatus.New, ticket.Status);
        Assert.Null(ticket.Resolution);
        Assert.NotEqual(Guid.Empty, ticket.Id);
        Assert.Equal(ticket.CreatedAt, ticket.UpdatedAt);
    }

    [Fact]
    public void Create_raises_exactly_one_created_event()
    {
        var ticket = AnyTicket();

        var raised = Assert.Single(ticket.DomainEvents);
        Assert.IsType<TicketCreated>(raised);
    }

    [Fact]
    public void Restore_raises_no_events()
    {
        // Loading from storage is not a business change. If Restore raised
        // TicketCreated, every read would re-notify every customer.
        var ticket = Ticket.Restore(
            Guid.NewGuid(), "Alan Turing", "alan@example.com", "Machine will not halt.",
            summary: "Halting problem.", imageUrl: null, status: TicketStatus.InProgress,
            resolution: "Investigating.", createdAt: DateTimeOffset.UtcNow, updatedAt: DateTimeOffset.UtcNow);

        Assert.Empty(ticket.DomainEvents);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_rejects_blank_required_text(string? blank)
    {
        Assert.Throws<ArgumentException>(() => Ticket.Create(blank!, "a@b.com", "Description."));
        Assert.Throws<ArgumentException>(() => Ticket.Create("Name", blank!, "Description."));
        Assert.Throws<ArgumentException>(() => Ticket.Create("Name", "a@b.com", blank!));
    }

    [Fact]
    public void ChangeStatus_to_a_different_value_reports_a_change_and_raises_one_event()
    {
        var ticket = AnyTicket();
        ticket.DrainDomainEvents();

        var changed = ticket.ChangeStatus(TicketStatus.InProgress);

        Assert.True(changed);
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        var raised = Assert.Single(ticket.DomainEvents);
        var statusChanged = Assert.IsType<TicketStatusChanged>(raised);
        Assert.Equal(TicketStatus.New, statusChanged.From);
        Assert.Equal(TicketStatus.InProgress, statusChanged.To);
    }

    [Fact]
    public void ChangeStatus_to_the_same_value_is_a_no_op()
    {
        var ticket = AnyTicket();
        ticket.DrainDomainEvents();

        var changed = ticket.ChangeStatus(TicketStatus.New);

        Assert.False(changed);
        Assert.Empty(ticket.DomainEvents);
    }

    [Fact]
    public void SetResolution_with_new_text_reports_a_change_and_raises_one_event()
    {
        var ticket = AnyTicket();
        ticket.DrainDomainEvents();

        var changed = ticket.SetResolution("Removed the moth.");

        Assert.True(changed);
        Assert.Equal("Removed the moth.", ticket.Resolution);
        Assert.IsType<TicketResolutionChanged>(Assert.Single(ticket.DomainEvents));
    }

    [Fact]
    public void SetResolution_with_identical_text_is_a_no_op()
    {
        var ticket = AnyTicket();
        ticket.SetResolution("Removed the moth.");
        ticket.DrainDomainEvents();

        var changed = ticket.SetResolution("Removed the moth.");

        Assert.False(changed);
        Assert.Empty(ticket.DomainEvents);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetResolution_treats_blank_as_still_unresolved(string? blank)
    {
        // The dataset stores "" for absent text while the domain uses null.
        // Moving between the two spellings must not count as a change.
        var ticket = AnyTicket();
        ticket.DrainDomainEvents();

        var changed = ticket.SetResolution(blank);

        Assert.False(changed);
        Assert.Null(ticket.Resolution);
        Assert.Empty(ticket.DomainEvents);
    }

    [Fact]
    public void SetSummary_never_raises_an_event()
    {
        // A summary is derived data; the customer has nothing to be told about.
        var ticket = AnyTicket();
        ticket.DrainDomainEvents();

        ticket.SetSummary("Moth in the compiler.");

        Assert.Equal("Moth in the compiler.", ticket.Summary);
        Assert.Empty(ticket.DomainEvents);
    }

    [Fact]
    public void Changing_something_advances_the_updated_timestamp()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var ticket = Ticket.Create("Grace", "grace@example.com", "Moth.", timeProvider: clock);
        var createdAt = ticket.UpdatedAt;

        clock.Advance(TimeSpan.FromMinutes(5));
        ticket.ChangeStatus(TicketStatus.Closed, clock);

        Assert.True(ticket.UpdatedAt > createdAt);
        Assert.Equal(createdAt, ticket.CreatedAt); // CreatedAt is immutable
    }

    [Fact]
    public void A_no_op_change_leaves_the_updated_timestamp_alone()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var ticket = Ticket.Create("Grace", "grace@example.com", "Moth.", timeProvider: clock);
        var before = ticket.UpdatedAt;

        clock.Advance(TimeSpan.FromMinutes(5));
        ticket.ChangeStatus(TicketStatus.New, clock);

        Assert.Equal(before, ticket.UpdatedAt);
    }

    [Fact]
    public void Draining_events_returns_them_once_and_then_empties()
    {
        var ticket = AnyTicket();

        var first = ticket.DrainDomainEvents();
        var second = ticket.DrainDomainEvents();

        Assert.Single(first);
        Assert.Empty(second);
    }

    /// <summary>Minimal controllable clock, so time-dependent rules are deterministic.</summary>
    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
