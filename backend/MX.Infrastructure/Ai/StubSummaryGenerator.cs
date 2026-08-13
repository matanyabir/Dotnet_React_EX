using Microsoft.Extensions.Options;
using MX.Application.Abstractions;
using MX.Infrastructure.Configuration;

namespace MX.Infrastructure.Ai;

/// <summary>
/// Summarises by taking the description's first sentence and trimming it to
/// length. No network, no API key, no cost.
///
/// This is the default so the AI-summary feature is visibly working the moment
/// the exercise is cloned, and so the whole test suite runs offline and fast.
/// It is deterministic, which is what makes it usable as a test double as well
/// as a real implementation — the same input always yields the same summary.
/// </summary>
public sealed class StubSummaryGenerator(IOptions<AiOptions> options) : ISummaryGenerator
{
    private static readonly char[] SentenceEndings = ['.', '!', '?'];

    private readonly int _maxLength = Math.Max(1, options.Value.MaxSummaryLength);

    public Task<string?> SummariseAsync(string description, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return Task.FromResult<string?>(null);
        }

        var text = description.Trim();

        // First sentence, when there is one and it is not the entire text.
        var end = text.IndexOfAny(SentenceEndings);
        if (end > 0 && end < text.Length - 1)
        {
            text = text[..(end + 1)];
        }

        return Task.FromResult<string?>(Truncate(text, _maxLength));
    }

    /// <summary>
    /// Cuts at a word boundary where possible, so the result reads as a clipped
    /// sentence rather than a severed word.
    /// </summary>
    private static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        var clipped = text[..maxLength];
        var lastSpace = clipped.LastIndexOf(' ');

        if (lastSpace > maxLength / 2)
        {
            clipped = clipped[..lastSpace];
        }

        return clipped.TrimEnd(',', ';', ':', ' ') + "…";
    }
}
