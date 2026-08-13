using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MX.Application.Abstractions;
using MX.Application.Tickets.Dtos;
using MX.Infrastructure.Email;

namespace MX.Api.Tests;

/// <summary>
/// The README's three email cases, end to end: on create, on status change, and
/// on resolution change — and, just as importantly, on nothing else.
///
/// Assertions run against the mock sender's recorded messages, which is why the
/// mock records rather than discards: "nothing threw" would prove nothing here.
/// </summary>
public sealed class EmailNotificationTests : IDisposable
{
    private readonly TicketApiFactory _factory = new();
    private readonly HttpClient _client;
    private HttpClient? _adminClient;

    public EmailNotificationTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _adminClient?.Dispose();
        _factory.Dispose();
    }

    private MockEmailSender Mailbox => _factory.Services.GetRequiredService<MockEmailSender>();

    private IReadOnlyList<EmailMessage> Sent => Mailbox.Sent;

    private async Task<HttpClient> AdminAsync() =>
        _adminClient ??= await _factory.CreateAuthenticatedClientAsync(TicketApiFactory.AdminEmail);

    private async Task<TicketDto> CreateTicketAsync(string email = "customer@example.com")
    {
        var response = await _client.PostAsJsonAsync("/api/tickets",
            new CreateTicketRequest("Sam Customer", email, "The dishwasher floods the kitchen."));

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TicketDto>(TicketApiFactory.Json))!;
    }

    private async Task<HttpResponseMessage> EditAsync(Guid id, UpdateTicketRequest request) =>
        await (await AdminAsync()).PutAsJsonAsync($"/api/tickets/{id}", request);

    // ---------------------------------------------------------------- create

    [Fact]
    public async Task Creating_a_ticket_sends_exactly_one_email_to_the_customer()
    {
        Mailbox.Clear();

        var ticket = await CreateTicketAsync("sam@example.com");

        var sent = Assert.Single(Sent);
        Assert.Equal("sam@example.com", sent.To);
        Assert.Contains(ticket.Id.ToString(), sent.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_confirmation_email_contains_the_tracking_link()
    {
        Mailbox.Clear();

        var ticket = await CreateTicketAsync();

        // The link points at the frontend route, not the API endpoint — a customer
        // following it wants the page, not JSON.
        Assert.Contains($"/tickets/{ticket.Id}", Assert.Single(Sent).Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_rejected_ticket_sends_nothing()
    {
        Mailbox.Clear();

        await _client.PostAsJsonAsync("/api/tickets",
            new CreateTicketRequest("", "not-an-email", ""));

        Assert.Empty(Sent);
    }

    // ---------------------------------------------------------- status change

    [Fact]
    public async Task Changing_the_status_sends_exactly_one_email()
    {
        var ticket = await CreateTicketAsync();
        Mailbox.Clear();

        await EditAsync(ticket.Id, new UpdateTicketRequest(Status: "In Progress"));

        var sent = Assert.Single(Sent);
        Assert.Contains("In Progress", sent.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Changing_the_status_twice_sends_two_emails()
    {
        var ticket = await CreateTicketAsync();
        Mailbox.Clear();

        await EditAsync(ticket.Id, new UpdateTicketRequest(Status: "In Progress"));
        await EditAsync(ticket.Id, new UpdateTicketRequest(Status: "Resolved"));

        Assert.Equal(2, Sent.Count);
    }

    // ------------------------------------------------------ resolution change

    [Fact]
    public async Task Changing_the_resolution_sends_exactly_one_email()
    {
        var ticket = await CreateTicketAsync();
        Mailbox.Clear();

        await EditAsync(ticket.Id, new UpdateTicketRequest(Resolution: "Unblocked the drain hose."));

        Assert.Contains("Unblocked the drain hose.", Assert.Single(Sent).Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Changing_status_and_resolution_together_sends_one_email_each()
    {
        var ticket = await CreateTicketAsync();
        Mailbox.Clear();

        await EditAsync(ticket.Id,
            new UpdateTicketRequest(Status: "Resolved", Resolution: "Unblocked the drain hose."));

        Assert.Equal(2, Sent.Count);
    }

    // ------------------------------------------------------------- no-op rule

    [Fact]
    public async Task Saving_without_changing_anything_sends_nothing()
    {
        // The rule that keeps this feature from becoming a nuisance: an admin
        // pressing Save twice must not email the customer twice.
        var ticket = await CreateTicketAsync();
        await EditAsync(ticket.Id, new UpdateTicketRequest(Status: "In Progress", Resolution: "Looking into it."));
        Mailbox.Clear();

        var response = await EditAsync(ticket.Id,
            new UpdateTicketRequest(Status: "In Progress", Resolution: "Looking into it."));

        response.EnsureSuccessStatusCode();
        Assert.Empty(Sent);
    }

    [Fact]
    public async Task An_empty_edit_sends_nothing()
    {
        var ticket = await CreateTicketAsync();
        Mailbox.Clear();

        await EditAsync(ticket.Id, new UpdateTicketRequest());

        Assert.Empty(Sent);
    }

    [Fact]
    public async Task Re_sending_the_same_resolution_text_sends_nothing()
    {
        var ticket = await CreateTicketAsync();
        await EditAsync(ticket.Id, new UpdateTicketRequest(Resolution: "Parts ordered."));
        Mailbox.Clear();

        await EditAsync(ticket.Id, new UpdateTicketRequest(Resolution: "Parts ordered."));

        Assert.Empty(Sent);
    }

    [Fact]
    public async Task An_unauthorised_edit_sends_nothing()
    {
        var ticket = await CreateTicketAsync();
        Mailbox.Clear();

        // No token: rejected before the service ever runs.
        await _client.PutAsJsonAsync($"/api/tickets/{ticket.Id}", new UpdateTicketRequest(Status: "Closed"));

        Assert.Empty(Sent);
    }

    [Fact]
    public async Task Reading_tickets_sends_nothing()
    {
        var ticket = await CreateTicketAsync();
        Mailbox.Clear();

        await _client.GetAsync("/api/tickets");
        await _client.GetAsync($"/api/tickets/{ticket.Id}");

        Assert.Empty(Sent);
    }

    [Fact]
    public async Task Restarting_against_an_existing_dataset_emails_nobody()
    {
        // Loading tickets from disk must not look like they were just created.
        // Ticket.Restore raising no events is what guarantees this; without it,
        // every startup would spam every customer in the file.
        using var freshFactory = new TicketApiFactory();
        using var client = freshFactory.CreateClient();

        await client.GetAsync("/api/tickets");

        Assert.Empty(freshFactory.Services.GetRequiredService<MockEmailSender>().Sent);
    }
}
