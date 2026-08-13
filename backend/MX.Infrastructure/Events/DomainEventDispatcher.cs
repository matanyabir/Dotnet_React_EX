using Microsoft.Extensions.Logging;
using MX.Application.Abstractions;
using MX.Domain.Common;

namespace MX.Infrastructure.Events;

/// <summary>
/// Non-generic view of a handler, so the dispatcher can hold a mixed list of them.
/// </summary>
public interface IDomainEventHandlerAdapter
{
    bool CanHandle(IDomainEvent domainEvent);

    Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken);
}

/// <summary>
/// Bridges a strongly-typed <see cref="IDomainEventHandler{TEvent}"/> to the
/// non-generic list the dispatcher iterates.
///
/// This exists so dispatch needs no reflection: the generic type argument is
/// supplied once at registration, and the cast below is guaranteed by the
/// <see cref="CanHandle"/> check immediately preceding it.
/// </summary>
internal sealed class DomainEventHandlerAdapter<TEvent>(IDomainEventHandler<TEvent> handler)
    : IDomainEventHandlerAdapter
    where TEvent : IDomainEvent
{
    public bool CanHandle(IDomainEvent domainEvent) => domainEvent is TEvent;

    public Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken) =>
        handler.HandleAsync((TEvent)domainEvent, cancellationToken);
}

/// <summary>
/// Delivers domain events to every handler registered for them, in process.
///
/// A handler that fails is logged and skipped rather than allowed to fail the
/// request. By the time events are dispatched the change is already saved, so
/// throwing here would report failure for something that actually succeeded —
/// the customer's ticket would exist while the response said it did not. An
/// undelivered notification is the lesser problem, and it leaves a log entry.
/// </summary>
public sealed class DomainEventDispatcher(
    IEnumerable<IDomainEventHandlerAdapter> handlers,
    ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    private readonly IReadOnlyList<IDomainEventHandlerAdapter> _handlers = handlers.ToArray();

    public async Task DispatchAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        foreach (var domainEvent in domainEvents)
        {
            foreach (var handler in _handlers.Where(h => h.CanHandle(domainEvent)))
            {
                try
                {
                    await handler.HandleAsync(domainEvent, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(
                        ex,
                        "Handler {Handler} failed for {Event}. The change was saved; the notification was not sent.",
                        handler.GetType().Name,
                        domainEvent.GetType().Name);
                }
            }
        }
    }
}
