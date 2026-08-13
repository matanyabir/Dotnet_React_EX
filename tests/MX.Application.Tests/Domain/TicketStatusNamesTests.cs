using MX.Domain.Tickets;

namespace MX.Application.Tests.Domain;

/// <summary>
/// The single source of truth for status spelling. Both the JSON file and the
/// HTTP API go through here, so a bug in this mapping corrupts the dataset.
/// </summary>
public class TicketStatusNamesTests
{
    [Theory]
    [InlineData(TicketStatus.New, "New")]
    [InlineData(TicketStatus.InProgress, "In Progress")]
    [InlineData(TicketStatus.Closed, "Closed")]
    [InlineData(TicketStatus.Resolved, "Resolved")]
    public void Emits_the_dataset_spelling(TicketStatus status, string expected) =>
        Assert.Equal(expected, status.ToDisplayName());

    [Theory]
    [InlineData("New", TicketStatus.New)]
    [InlineData("In Progress", TicketStatus.InProgress)]
    [InlineData("in progress", TicketStatus.InProgress)]
    [InlineData("InProgress", TicketStatus.InProgress)]   // tolerated on input
    [InlineData("  Closed  ", TicketStatus.Closed)]
    [InlineData("RESOLVED", TicketStatus.Resolved)]
    public void Accepts_reasonable_input_spellings(string input, TicketStatus expected)
    {
        Assert.True(TicketStatusNames.TryParse(input, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Pending")]
    [InlineData("In-Progress")]
    public void Rejects_unknown_values(string? input) =>
        Assert.False(TicketStatusNames.TryParse(input, out _));

    [Fact]
    public void Every_enum_member_round_trips_through_its_display_name()
    {
        foreach (var status in Enum.GetValues<TicketStatus>())
        {
            Assert.True(TicketStatusNames.TryParse(status.ToDisplayName(), out var parsed));
            Assert.Equal(status, parsed);
        }
    }

    [Fact]
    public void All_lists_every_enum_member()
    {
        // Guards the frontend dropdown against a status being added to the enum
        // but forgotten here.
        Assert.Equal(Enum.GetValues<TicketStatus>().Length, TicketStatusNames.All.Count);
    }
}
