using Microsoft.Extensions.Logging;
using MX.Application.Abstractions;

namespace MX.Infrastructure.Ai;

/// <summary>
/// Wraps any <see cref="ISummaryGenerator"/> and makes its no-throw, no-hang
/// contract true.
///
/// A decorator rather than a try/catch inside each provider: resilience is one
/// concern implemented once, and a new provider gets it for free without
/// remembering to. It is also why <c>TicketService</c> carries no error handling
/// around the summary call — that guarantee is enforced here.
///
/// A summary is a convenience. A slow or broken AI provider must cost the
/// summary and nothing else — never the customer's ticket.
/// </summary>
public sealed class ResilientSummaryGenerator(
    ISummaryGenerator inner,
    TimeSpan timeout,
    ILogger<ResilientSummaryGenerator> logger) : ISummaryGenerator
{
    public async Task<string?> SummariseAsync(string description, CancellationToken cancellationToken = default)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(timeout);

        try
        {
            return await inner.SummariseAsync(description, budget.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The *caller* gave up — the customer closed the tab, the request was
            // aborted. That is not a summary failure to swallow; let it propagate
            // so the abandoned work stops here rather than running on.
            throw;
        }
        catch (OperationCanceledException)
        {
            // Only our own budget expired.
            logger.LogWarning(
                "Summary generation exceeded {Timeout}. Continuing without a summary.",
                timeout);

            return null;
        }
        catch (Exception ex)
        {
            // Deliberately broad. Every provider failure — network, auth, quota,
            // a malformed response — has the same correct outcome here: no
            // summary, ticket unaffected, one log line to investigate later.
            logger.LogError(ex, "Summary generation failed. Continuing without a summary.");

            return null;
        }
    }
}
