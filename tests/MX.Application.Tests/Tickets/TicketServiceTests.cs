using MX.Application.Abstractions;
using MX.Application.Common;
using MX.Application.Tickets;
using MX.Application.Tickets.Dtos;
using MX.Domain.Common;
using MX.Domain.Tickets;
using MX.Domain.Tickets.Events;
using NSubstitute;

namespace MX.Application.Tests.Tickets;

/// <summary>
/// Unit tests for the ticket use cases. Every collaborator is substituted, so
/// these run without a file, a network, or a web host — a failure here points at
/// the orchestration logic and nothing else.
/// </summary>
public class TicketServiceTests
{
    private readonly ITicketRepository _repository = Substitute.For<ITicketRepository>();
    private readonly ISummaryGenerator _summaries = Substitute.For<ISummaryGenerator>();
    private readonly IDomainEventDispatcher _dispatcher = Substitute.For<IDomainEventDispatcher>();
    private readonly List<IDomainEvent> _dispatched = [];

    private readonly TicketService _service;

    public TicketServiceTests()
    {
        _dispatcher
            .DispatchAsync(Arg.Do<IEnumerable<IDomainEvent>>(events => _dispatched.AddRange(events)),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _summaries.SummariseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        _service = new TicketService(_repository, _summaries, _dispatcher);
    }

    private static Ticket Seeded(
        string name = "John Doe",
        string description = "Laptop overheats badly.",
        TicketStatus status = TicketStatus.New,
        string? resolution = null,
        int daysOld = 0)
    {
        var created = DateTimeOffset.UtcNow.AddDays(-daysOld);

        return Ticket.Restore(
            Guid.NewGuid(), name, "john@example.com", description,
            summary: null, imageUrl: null, status: status, resolution: resolution,
            createdAt: created, updatedAt: created);
    }

    private void GivenStored(params Ticket[] tickets)
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(tickets);

        foreach (var ticket in tickets)
        {
            _repository.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>()).Returns(ticket);
        }
    }

    // ---------------------------------------------------------------- listing

    [Fact]
    public async Task List_returns_every_ticket_when_unfiltered()
    {
        GivenStored(Seeded(), Seeded(), Seeded());

        var result = await _service.ListAsync(TicketQuery.Unfiltered);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Count);
    }

    [Fact]
    public async Task List_orders_newest_first()
    {
        GivenStored(
            Seeded(name: "Oldest", daysOld: 10),
            Seeded(name: "Newest", daysOld: 0),
            Seeded(name: "Middle", daysOld: 5));

        var result = await _service.ListAsync(TicketQuery.Unfiltered);

        Assert.Equal(["Newest", "Middle", "Oldest"], result.Value!.Select(t => t.Name));
    }

    [Fact]
    public async Task List_filters_by_status()
    {
        GivenStored(
            Seeded(name: "A", status: TicketStatus.New),
            Seeded(name: "B", status: TicketStatus.InProgress),
            Seeded(name: "C", status: TicketStatus.InProgress));

        var result = await _service.ListAsync(new TicketQuery(Status: "In Progress"));

        Assert.Equal(2, result.Value!.Count);
        Assert.All(result.Value!, t => Assert.Equal("In Progress", t.Status));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("All")]
    [InlineData("all")]
    public async Task List_treats_blank_or_All_as_no_status_filter(string? status)
    {
        // "All" is the literal value the mockup's dropdown starts on.
        GivenStored(Seeded(status: TicketStatus.New), Seeded(status: TicketStatus.Closed));

        var result = await _service.ListAsync(new TicketQuery(Status: status));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
    }

    [Fact]
    public async Task List_rejects_an_unknown_status()
    {
        GivenStored(Seeded());

        var result = await _service.ListAsync(new TicketQuery(Status: "Pending"));

        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task List_searches_the_customer_name_ignoring_case()
    {
        GivenStored(Seeded(name: "Emily Johnson"), Seeded(name: "John Doe"), Seeded(name: "Jane Smith"));

        var result = await _service.ListAsync(new TicketQuery(Search: "jo"));

        // Matches "Emily Johnson" and "John Doe", not "Jane Smith".
        Assert.Equal(2, result.Value!.Count);
    }

    [Fact]
    public async Task List_searches_the_description_too()
    {
        GivenStored(
            Seeded(name: "Someone", description: "The washing machine will not drain."),
            Seeded(name: "Another", description: "Laptop overheats."));

        var result = await _service.ListAsync(new TicketQuery(Search: "washing"));

        var match = Assert.Single(result.Value!);
        Assert.Equal("Someone", match.Name);
    }

    [Fact]
    public async Task List_applies_status_and_search_together()
    {
        GivenStored(
            Seeded(name: "Match", description: "printer jam", status: TicketStatus.Closed),
            Seeded(name: "Wrong status", description: "printer jam", status: TicketStatus.New),
            Seeded(name: "Wrong text", description: "screen flicker", status: TicketStatus.Closed));

        var result = await _service.ListAsync(new TicketQuery(Status: "Closed", Search: "printer"));

        var match = Assert.Single(result.Value!);
        Assert.Equal("Match", match.Name);
    }

    [Fact]
    public async Task List_exposes_status_as_the_display_string()
    {
        // The frontend and the JSON file must see "In Progress", never "InProgress".
        GivenStored(Seeded(status: TicketStatus.InProgress));

        var result = await _service.ListAsync(TicketQuery.Unfiltered);

        Assert.Equal("In Progress", result.Value!.Single().Status);
    }

    // ------------------------------------------------------------- single get

    [Fact]
    public async Task Get_returns_the_ticket_when_it_exists()
    {
        var ticket = Seeded();
        GivenStored(ticket);

        var result = await _service.GetByIdAsync(ticket.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(ticket.Id, result.Value!.Id);
    }

    [Fact]
    public async Task Get_reports_not_found_for_an_unknown_id()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Ticket?)null);

        var result = await _service.GetByIdAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
    }

    // ---------------------------------------------------------------- create

    [Fact]
    public async Task Create_stores_the_ticket_and_announces_it()
    {
        var request = new CreateTicketRequest("Ada Lovelace", "ada@example.com", "The printer is on fire.");

        var result = await _service.CreateAsync(request);

        Assert.True(result.IsSuccess);
        await _repository.Received(1).AddAsync(Arg.Any<Ticket>(), Arg.Any<CancellationToken>());
        Assert.IsType<TicketCreated>(Assert.Single(_dispatched));
    }

    [Fact]
    public async Task Create_starts_the_ticket_as_New_regardless_of_what_was_sent()
    {
        var result = await _service.CreateAsync(
            new CreateTicketRequest("Ada", "ada@example.com", "Printer on fire."));

        Assert.Equal("New", result.Value!.Status);
        Assert.Null(result.Value!.Resolution);
    }

    [Fact]
    public async Task Create_attaches_the_generated_summary()
    {
        _summaries.SummariseAsync("The printer is on fire.", Arg.Any<CancellationToken>())
            .Returns("Printer ablaze.");

        var result = await _service.CreateAsync(
            new CreateTicketRequest("Ada", "ada@example.com", "The printer is on fire."));

        Assert.Equal("Printer ablaze.", result.Value!.Summary);
    }

    [Fact]
    public async Task Create_still_succeeds_when_no_summary_is_available()
    {
        // The port returns null when the AI provider is unreachable or disabled.
        _summaries.SummariseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);

        var result = await _service.CreateAsync(
            new CreateTicketRequest("Ada", "ada@example.com", "Printer on fire."));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Summary);
    }

    [Fact]
    public async Task Create_carries_through_an_uploaded_image_path()
    {
        var result = await _service.CreateAsync(
            new CreateTicketRequest("Ada", "ada@example.com", "Printer on fire.", ImageUrl: "uploads/fire.jpg"));

        Assert.Equal("uploads/fire.jpg", result.Value!.ImageUrl);
    }

    [Theory]
    [InlineData("", "ada@example.com", "Description here.")]
    [InlineData("   ", "ada@example.com", "Description here.")]
    [InlineData("Ada", "", "Description here.")]
    [InlineData("Ada", "not-an-email", "Description here.")]
    [InlineData("Ada", "ada@example.com", "")]
    public async Task Create_rejects_bad_input_without_touching_storage(
        string name, string email, string description)
    {
        var result = await _service.CreateAsync(new CreateTicketRequest(name, email, description));

        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.NotEmpty(result.Errors);

        // Nothing was written and nobody was emailed about a ticket that does not exist.
        await _repository.DidNotReceive().AddAsync(Arg.Any<Ticket>(), Arg.Any<CancellationToken>());
        Assert.Empty(_dispatched);
    }

    [Fact]
    public async Task Create_reports_every_validation_problem_at_once()
    {
        // One round trip should tell the user everything that is wrong.
        var result = await _service.CreateAsync(new CreateTicketRequest("", "nope", ""));

        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public async Task Create_does_not_ask_the_AI_about_input_it_has_already_rejected()
    {
        await _service.CreateAsync(new CreateTicketRequest("", "", ""));

        await _summaries.DidNotReceive().SummariseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------- update

    [Fact]
    public async Task Update_reports_not_found_for_an_unknown_id()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Ticket?)null);

        var result = await _service.UpdateAsync(Guid.NewGuid(), new UpdateTicketRequest(Status: "Closed"));

        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task Update_persists_and_announces_a_status_change()
    {
        var ticket = Seeded(status: TicketStatus.New);
        GivenStored(ticket);

        var result = await _service.UpdateAsync(ticket.Id, new UpdateTicketRequest(Status: "In Progress"));

        Assert.Equal("In Progress", result.Value!.Status);
        await _repository.Received(1).UpdateAsync(ticket, Arg.Any<CancellationToken>());
        var raised = Assert.IsType<TicketStatusChanged>(Assert.Single(_dispatched));
        Assert.Equal(TicketStatus.New, raised.From);
        Assert.Equal(TicketStatus.InProgress, raised.To);
    }

    [Fact]
    public async Task Update_persists_and_announces_a_resolution_change()
    {
        var ticket = Seeded();
        GivenStored(ticket);

        await _service.UpdateAsync(ticket.Id, new UpdateTicketRequest(Resolution: "Replaced the fan."));

        await _repository.Received(1).UpdateAsync(ticket, Arg.Any<CancellationToken>());
        Assert.IsType<TicketResolutionChanged>(Assert.Single(_dispatched));
    }

    [Fact]
    public async Task Update_changing_both_fields_saves_once_and_raises_two_events()
    {
        var ticket = Seeded(status: TicketStatus.New);
        GivenStored(ticket);

        await _service.UpdateAsync(ticket.Id,
            new UpdateTicketRequest(Status: "Resolved", Resolution: "Replaced the fan."));

        await _repository.Received(1).UpdateAsync(ticket, Arg.Any<CancellationToken>());
        Assert.Equal(2, _dispatched.Count);
        Assert.Single(_dispatched.OfType<TicketStatusChanged>());
        Assert.Single(_dispatched.OfType<TicketResolutionChanged>());
    }

    [Fact]
    public async Task Update_with_no_actual_change_neither_saves_nor_notifies()
    {
        // Pressing Save twice must not email the customer a second time — the
        // single most visible symptom of getting the change detection wrong.
        var ticket = Seeded(status: TicketStatus.InProgress, resolution: "Parts ordered.");
        GivenStored(ticket);

        var result = await _service.UpdateAsync(ticket.Id,
            new UpdateTicketRequest(Status: "In Progress", Resolution: "Parts ordered."));

        Assert.True(result.IsSuccess);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Ticket>(), Arg.Any<CancellationToken>());
        Assert.Empty(_dispatched);
    }

    [Fact]
    public async Task Update_leaves_omitted_fields_untouched()
    {
        var ticket = Seeded(status: TicketStatus.InProgress, resolution: "Parts ordered.");
        GivenStored(ticket);

        var result = await _service.UpdateAsync(ticket.Id, new UpdateTicketRequest(Status: "Closed"));

        Assert.Equal("Closed", result.Value!.Status);
        Assert.Equal("Parts ordered.", result.Value!.Resolution);
        Assert.Single(_dispatched);
    }

    [Fact]
    public async Task Update_with_an_empty_resolution_clears_it()
    {
        // "" is an explicit clear, distinct from null meaning "leave alone".
        var ticket = Seeded(resolution: "Parts ordered.");
        GivenStored(ticket);

        var result = await _service.UpdateAsync(ticket.Id, new UpdateTicketRequest(Resolution: ""));

        Assert.Null(result.Value!.Resolution);
        Assert.IsType<TicketResolutionChanged>(Assert.Single(_dispatched));
    }

    [Fact]
    public async Task Update_with_an_empty_request_is_a_no_op()
    {
        var ticket = Seeded();
        GivenStored(ticket);

        var result = await _service.UpdateAsync(ticket.Id, new UpdateTicketRequest());

        Assert.True(result.IsSuccess);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Ticket>(), Arg.Any<CancellationToken>());
        Assert.Empty(_dispatched);
    }

    [Fact]
    public async Task Update_rejects_an_unknown_status_before_loading_the_ticket()
    {
        var result = await _service.UpdateAsync(Guid.NewGuid(), new UpdateTicketRequest(Status: "Pending"));

        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        await _repository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_accepts_the_spaceless_status_spelling()
    {
        var ticket = Seeded(status: TicketStatus.New);
        GivenStored(ticket);

        var result = await _service.UpdateAsync(ticket.Id, new UpdateTicketRequest(Status: "InProgress"));

        // Lenient on input, canonical on output.
        Assert.Equal("In Progress", result.Value!.Status);
    }
}
