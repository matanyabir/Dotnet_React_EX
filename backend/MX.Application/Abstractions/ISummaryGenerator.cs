namespace MX.Application.Abstractions;

/// <summary>
/// Produces a short précis of a ticket description (the README's AI-summary bonus).
///
/// Contract: implementations MUST NOT throw and MUST NOT hang indefinitely.
/// A summary is a nice-to-have, and an unreachable AI provider must never stop a
/// customer from filing a ticket — so unavailability is expressed by returning
/// <c>null</c>, not by an exception. <c>ResilientSummaryGenerator</c> enforces
/// this for providers that cannot promise it themselves, which is why
/// <c>TicketService</c> carries no try/catch of its own.
/// </summary>
public interface ISummaryGenerator
{
    /// <returns>A short summary, or <c>null</c> when none could be produced.</returns>
    Task<string?> SummariseAsync(string description, CancellationToken cancellationToken = default);
}
