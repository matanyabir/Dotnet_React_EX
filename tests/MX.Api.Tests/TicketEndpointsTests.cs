using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MX.Application.Tickets.Dtos;

namespace MX.Api.Tests;

/// <summary>
/// Exercises the endpoints over real HTTP against a real dataset file. These
/// cover what unit tests structurally cannot: routing, model binding, status
/// codes, the ProblemDetails shape, and that writes actually reach disk.
/// </summary>
public sealed class TicketEndpointsTests : IDisposable
{
    private readonly TicketApiFactory _factory = new();
    private readonly HttpClient _client;
    private HttpClient? _adminClient;

    public TicketEndpointsTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _adminClient?.Dispose();
        _factory.Dispose();
    }

    /// <summary>A signed-in admin client. Editing is admin-only from Stage 5 on.</summary>
    private async Task<HttpClient> AdminAsync() =>
        _adminClient ??= await _factory.CreateAuthenticatedClientAsync(TicketApiFactory.AdminEmail);

    private static CreateTicketRequest AnyNewTicket(
        string name = "Ada Lovelace",
        string email = "ada@example.com",
        string description = "The printer is on fire and smells of toast.") =>
        new(name, email, description);

    private async Task<TicketDto> CreateAsync(CreateTicketRequest request)
    {
        var response = await _client.PostAsJsonAsync("/api/tickets", request);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<TicketDto>(TicketApiFactory.Json))!;
    }

    // ----------------------------------------------------------------- listing

    [Fact]
    public async Task Get_tickets_returns_the_seeded_dataset()
    {
        var tickets = await _client.GetFromJsonAsync<List<TicketDto>>("/api/tickets", TicketApiFactory.Json);

        Assert.NotNull(tickets);
        Assert.Equal(5, tickets.Count);
    }

    [Fact]
    public async Task Get_tickets_serialises_the_status_with_its_space()
    {
        // End-to-end proof that "In Progress" survives file -> domain -> DTO -> JSON.
        var raw = await _client.GetStringAsync("/api/tickets");

        Assert.Contains("\"In Progress\"", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("\"InProgress\"", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_tickets_filters_by_status()
    {
        var tickets = await _client.GetFromJsonAsync<List<TicketDto>>(
            "/api/tickets?status=In%20Progress", TicketApiFactory.Json);

        Assert.NotEmpty(tickets!);
        Assert.All(tickets!, t => Assert.Equal("In Progress", t.Status));
    }

    [Fact]
    public async Task Get_tickets_filters_by_free_text()
    {
        var tickets = await _client.GetFromJsonAsync<List<TicketDto>>(
            "/api/tickets?search=washing", TicketApiFactory.Json);

        var match = Assert.Single(tickets!);
        Assert.Contains("washing", match.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_tickets_rejects_an_unknown_status_with_a_validation_problem()
    {
        var response = await _client.GetAsync("/api/tickets?status=Pending");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Get_statuses_returns_the_domain_vocabulary()
    {
        var statuses = await _client.GetFromJsonAsync<List<string>>("/api/tickets/statuses", TicketApiFactory.Json);

        Assert.Equal(["New", "In Progress", "Closed", "Resolved"], statuses);
    }

    // -------------------------------------------------------------- single get

    [Fact]
    public async Task Get_ticket_by_id_returns_it()
    {
        var all = await _client.GetFromJsonAsync<List<TicketDto>>("/api/tickets", TicketApiFactory.Json);
        var expected = all![0];

        var actual = await _client.GetFromJsonAsync<TicketDto>(
            $"/api/tickets/{expected.Id}", TicketApiFactory.Json);

        Assert.Equal(expected.Id, actual!.Id);
        Assert.Equal(expected.Name, actual.Name);
    }

    [Fact]
    public async Task Get_ticket_by_an_unknown_id_returns_404_problem_details()
    {
        var response = await _client.GetAsync($"/api/tickets/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Get_ticket_by_a_non_guid_id_does_not_match_the_route()
    {
        // The :guid constraint keeps malformed ids away from the handler entirely.
        var response = await _client.GetAsync("/api/tickets/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ----------------------------------------------------------------- create

    [Fact]
    public async Task Post_creates_a_ticket_and_returns_201_with_a_location()
    {
        var response = await _client.PostAsJsonAsync("/api/tickets", AnyNewTicket());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<TicketDto>(TicketApiFactory.Json);
        Assert.Equal($"/api/tickets/{created!.Id}", response.Headers.Location?.ToString());
        Assert.Equal("New", created.Status);
    }

    [Fact]
    public async Task A_created_ticket_is_readable_at_its_own_url()
    {
        // The tracking link in the confirmation email depends on exactly this.
        var created = await CreateAsync(AnyNewTicket());

        var fetched = await _client.GetFromJsonAsync<TicketDto>(
            $"/api/tickets/{created.Id}", TicketApiFactory.Json);

        Assert.Equal(created.Id, fetched!.Id);
    }

    [Fact]
    public async Task A_created_ticket_reaches_the_json_file_on_disk()
    {
        var created = await CreateAsync(AnyNewTicket(name: "Persisted Person"));

        var onDisk = await File.ReadAllTextAsync(_factory.DataFilePath);

        Assert.Contains(created.Id.ToString(), onDisk, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Persisted Person", onDisk, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_rejects_invalid_input_with_a_validation_problem()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/tickets", AnyNewTicket(name: "", email: "not-an-email"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = problem.RootElement.GetProperty("errors").GetProperty("request");

        Assert.Equal(2, errors.GetArrayLength());
    }

    [Fact]
    public async Task Post_does_not_persist_a_rejected_ticket()
    {
        var before = await File.ReadAllTextAsync(_factory.DataFilePath);

        await _client.PostAsJsonAsync("/api/tickets", AnyNewTicket(email: "nope"));

        Assert.Equal(before, await File.ReadAllTextAsync(_factory.DataFilePath));
    }

    // ----------------------------------------------------------------- update

    [Fact]
    public async Task Put_updates_the_status()
    {
        var created = await CreateAsync(AnyNewTicket());

        var response = await (await AdminAsync()).PutAsJsonAsync(
            $"/api/tickets/{created.Id}", new UpdateTicketRequest(Status: "In Progress"));

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<TicketDto>(TicketApiFactory.Json);

        Assert.Equal("In Progress", updated!.Status);
        Assert.True(updated.UpdatedAt >= created.UpdatedAt);
    }

    [Fact]
    public async Task Put_updates_the_resolution()
    {
        var created = await CreateAsync(AnyNewTicket());

        var response = await (await AdminAsync()).PutAsJsonAsync(
            $"/api/tickets/{created.Id}", new UpdateTicketRequest(Resolution: "Extinguished the printer."));

        var updated = await response.Content.ReadFromJsonAsync<TicketDto>(TicketApiFactory.Json);

        Assert.Equal("Extinguished the printer.", updated!.Resolution);
    }

    [Fact]
    public async Task Put_leaves_the_field_it_was_not_given_alone()
    {
        var created = await CreateAsync(AnyNewTicket());
        await (await AdminAsync()).PutAsJsonAsync(
            $"/api/tickets/{created.Id}", new UpdateTicketRequest(Resolution: "Parts ordered."));

        var response = await (await AdminAsync()).PutAsJsonAsync(
            $"/api/tickets/{created.Id}", new UpdateTicketRequest(Status: "Closed"));

        var updated = await response.Content.ReadFromJsonAsync<TicketDto>(TicketApiFactory.Json);

        Assert.Equal("Closed", updated!.Status);
        Assert.Equal("Parts ordered.", updated.Resolution);
    }

    [Fact]
    public async Task Put_changes_survive_a_reread_from_disk()
    {
        var created = await CreateAsync(AnyNewTicket());
        await (await AdminAsync()).PutAsJsonAsync(
            $"/api/tickets/{created.Id}", new UpdateTicketRequest(Status: "Resolved", Resolution: "Done."));

        var onDisk = await File.ReadAllTextAsync(_factory.DataFilePath);

        Assert.Contains("\"status\": \"Resolved\"", onDisk, StringComparison.Ordinal);
        Assert.Contains("Done.", onDisk, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Put_to_an_unknown_id_returns_404()
    {
        var response = await (await AdminAsync()).PutAsJsonAsync(
            $"/api/tickets/{Guid.NewGuid()}", new UpdateTicketRequest(Status: "Closed"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_with_an_unknown_status_returns_400()
    {
        var created = await CreateAsync(AnyNewTicket());

        var response = await (await AdminAsync()).PutAsJsonAsync(
            $"/api/tickets/{created.Id}", new UpdateTicketRequest(Status: "Pending"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------------ round trips

    [Fact]
    public async Task The_dataset_file_stays_loadable_after_a_write()
    {
        // Guards the whole persistence format: if a write corrupted the file or
        // changed the status spelling, the next read would fail or come back short.
        await CreateAsync(AnyNewTicket());

        var tickets = await _client.GetFromJsonAsync<List<TicketDto>>("/api/tickets", TicketApiFactory.Json);

        Assert.Equal(6, tickets!.Count);
        Assert.Contains(tickets, t => t.Status == "In Progress");
    }
}
