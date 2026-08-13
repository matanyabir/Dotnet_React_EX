using MX.Application.Abstractions;

namespace MX.Infrastructure.Ai;

/// <summary>
/// Produces no summary at all — the "AI summary is switched off" option.
///
/// A real implementation of the port rather than a stub that throws: turning the
/// feature off must be indistinguishable, from the caller's side, from a provider
/// that had nothing useful to say. Stage 7 adds the generating implementations
/// alongside it and selects between them by configuration.
/// </summary>
public sealed class NullSummaryGenerator : ISummaryGenerator
{
    public Task<string?> SummariseAsync(string description, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}
