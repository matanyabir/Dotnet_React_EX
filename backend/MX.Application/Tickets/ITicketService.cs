using MX.Application.Common;
using MX.Application.Tickets.Dtos;

namespace MX.Application.Tickets;

/// <summary>
/// The four ticket use cases the README calls for. Everything returns
/// <see cref="Result{TValue}"/>, so callers handle "not found" and "invalid"
/// without exception handling.
/// </summary>
public interface ITicketService
{
    /// <summary>All tickets matching the filters, newest first.</summary>
    Task<Result<IReadOnlyList<TicketDto>>> ListAsync(
        TicketQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>A single ticket by its unique id — the <c>/tickets/{id}</c> deep link.</summary>
    Task<Result<TicketDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Files a new ticket, attaching an AI summary when one is available.</summary>
    Task<Result<TicketDto>> CreateAsync(
        CreateTicketRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Updates status and/or resolution text. Admin-only at the HTTP layer.</summary>
    Task<Result<TicketDto>> UpdateAsync(
        Guid id,
        UpdateTicketRequest request,
        CancellationToken cancellationToken = default);
}
