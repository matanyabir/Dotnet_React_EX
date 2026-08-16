using MX.Application.Abstractions;
using MX.Application.Common;
using MX.Application.Tickets.Dtos;
using MX.Domain.Tickets;

namespace MX.Application.Tickets;

/// <summary>
/// Orchestrates the ticket use cases.
///
/// It coordinates collaborators rather than doing the work itself: the entity
/// decides what counts as a change, the repository stores, the summary generator
/// summarises, and the dispatcher tells whoever cares. Nothing here knows that
/// notifications are emails or that storage is a JSON file.
/// </summary>
public sealed class TicketService : ITicketService
{
    private readonly ITicketRepository _repository;
    private readonly ISummaryGenerator _summaryGenerator;
    private readonly IDomainEventDispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;

    public TicketService(
        ITicketRepository repository,
        ISummaryGenerator summaryGenerator,
        IDomainEventDispatcher dispatcher,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(summaryGenerator);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _repository = repository;
        _summaryGenerator = summaryGenerator;
        _dispatcher = dispatcher;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Result<PagedResult<TicketDto>>> ListAsync(
        TicketQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Everything wrong with the query is reported at once, so a request with a
        // bad status *and* a bad page does not need two round trips to fix.
        var errors = new List<string>();

        var status = default(TicketStatus);
        if (query.HasStatusFilter && !TicketStatusNames.TryParse(query.Status, out status))
        {
            errors.Add(
                $"'{query.Status}' is not a valid status. Expected one of: " +
                $"{string.Join(", ", TicketStatusNames.All)}.");
        }

        errors.AddRange(query.PagingErrors);

        if (errors.Count > 0)
        {
            return Result<PagedResult<TicketDto>>.Invalid(errors);
        }

        var tickets = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var matches = tickets.AsEnumerable();

        if (query.HasStatusFilter)
        {
            matches = matches.Where(t => t.Status == status);
        }

        if (query.HasSearchFilter)
        {
            var term = query.Search!.Trim();

            // The README asks for a text search "by name or description".
            matches = matches.Where(t =>
                t.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        // Newest first: a support queue is read top-down, and the freshest
        // complaint is the one nobody has looked at yet.
        //
        // LINQ's sort is documented stable, which paging now depends on: with an
        // unstable sort, two tickets sharing a timestamp could swap places between
        // the request for page 1 and the request for page 2, so one of them would
        // appear twice and the other not at all.
        var ordered = matches
            .OrderByDescending(t => t.CreatedAt)
            .ToArray();

        // Counted before the page is cut, because the pager needs the size of the
        // whole match — not of the slice the caller can see.
        var dtos = ordered
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(t => t.ToDto())
            .ToArray();

        return Result<PagedResult<TicketDto>>.Success(
            new PagedResult<TicketDto>(dtos, query.Page, query.PageSize, ordered.Length));
    }

    public async Task<Result<TicketDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ticket = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        return ticket is null
            ? Result<TicketDto>.NotFound($"No ticket with id {id} exists.")
            : Result<TicketDto>.Success(ticket.ToDto());
    }

    public async Task<Result<TicketDto>> CreateAsync(
        CreateTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = TicketValidator.Validate(request);
        if (errors.Count > 0)
        {
            return Result<TicketDto>.Invalid(errors);
        }

        // Summarising before construction keeps the ticket valid from its first
        // moment. The port guarantees this call neither throws nor hangs, so an
        // unavailable AI provider costs the summary and nothing else.
        var summary = await _summaryGenerator
            .SummariseAsync(request.Description, cancellationToken)
            .ConfigureAwait(false);

        var ticket = Ticket.Create(
            name: request.Name,
            email: request.Email,
            description: request.Description,
            summary: summary,
            imageUrl: request.ImageUrl,
            timeProvider: _timeProvider);

        await _repository.AddAsync(ticket, cancellationToken).ConfigureAwait(false);
        await DispatchAsync(ticket, cancellationToken).ConfigureAwait(false);

        return Result<TicketDto>.Success(ticket.ToDto());
    }

    public async Task<Result<TicketDto>> UpdateAsync(
        Guid id,
        UpdateTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = TicketValidator.Validate(request);
        if (errors.Count > 0)
        {
            return Result<TicketDto>.Invalid(errors);
        }

        var ticket = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (ticket is null)
        {
            return Result<TicketDto>.NotFound($"No ticket with id {id} exists.");
        }

        var changed = false;

        // Null means "leave alone". Validation above already proved this parses.
        if (request.Status is not null && TicketStatusNames.TryParse(request.Status, out var newStatus))
        {
            changed |= ticket.ChangeStatus(newStatus, _timeProvider);
        }

        if (request.Resolution is not null)
        {
            changed |= ticket.SetResolution(request.Resolution, _timeProvider);
        }

        // Saving an unchanged ticket would rewrite the dataset and, worse, tell the
        // customer something happened. Pressing Save twice must stay silent.
        if (changed)
        {
            await _repository.UpdateAsync(ticket, cancellationToken).ConfigureAwait(false);
            await DispatchAsync(ticket, cancellationToken).ConfigureAwait(false);
        }

        return Result<TicketDto>.Success(ticket.ToDto());
    }

    /// <summary>
    /// Drains and publishes whatever the entity recorded. Called only after a
    /// successful save, so no notification can describe a change that was lost.
    /// </summary>
    private async Task DispatchAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        var events = ticket.DrainDomainEvents();
        if (events.Count == 0)
        {
            return;
        }

        await _dispatcher.DispatchAsync(events, cancellationToken).ConfigureAwait(false);
    }
}
