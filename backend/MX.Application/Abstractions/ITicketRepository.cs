using MX.Domain.Tickets;

namespace MX.Application.Abstractions;

/// <summary>
/// Persistence port for tickets. Declared here — in the layer that consumes it —
/// and implemented in MX.Infrastructure, so the application depends on the
/// abstraction while the storage detail depends on the application.
///
/// Filtering is intentionally absent: the dataset is small enough to filter in
/// memory, and keeping query logic in <c>TicketService</c> makes it unit-testable
/// without any storage at all.
/// </summary>
public interface ITicketRepository
{
    Task<IReadOnlyList<Ticket>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Ticket ticket, CancellationToken cancellationToken = default);

    /// <summary>Persists changes made to an already-stored ticket.</summary>
    Task UpdateAsync(Ticket ticket, CancellationToken cancellationToken = default);
}
