using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MX.Application.Abstractions;
using MX.Infrastructure.Configuration;

namespace MX.Infrastructure.Ai;

/// <summary>
/// Writes the ticket summary with Claude — the README's AI-summary bonus.
///
/// A single request/response call: summarisation needs no tools, no agent loop,
/// and no conversation state, so the simplest surface is the right one.
///
/// Interchangeable with <see cref="StubSummaryGenerator"/> through
/// <see cref="ISummaryGenerator"/>, selected by the "Ai:Provider" setting.
/// </summary>
public sealed class ClaudeSummaryGenerator : ISummaryGenerator
{
    /// <summary>
    /// Roomy on purpose. On Claude Opus 5 thinking is on by default and
    /// MaxTokens caps thinking *plus* the reply, so a tight limit truncates the
    /// answer rather than saving anything — unused tokens are never billed.
    /// </summary>
    private const int MaxTokens = 4096;

    private const string SystemPrompt =
        """
        You summarise customer support tickets for a support agent's queue.

        Reply with one short sentence naming the product or system and what is
        wrong with it — nothing else. No preamble, no quotes, no bullet points,
        no closing remark. Aim for under 15 words.

        Example description: "My laptop gets very hot and shuts itself down
        after about ten minutes of use, every single time."
        Example reply: Laptop overheating and shutting down under load.
        """;

    private readonly AnthropicClient _client;
    private readonly AiOptions _options;
    private readonly ILogger<ClaudeSummaryGenerator> _logger;

    public ClaudeSummaryGenerator(IOptions<AiOptions> options, ILogger<ClaudeSummaryGenerator> logger)
    {
        _options = options.Value;
        _logger = logger;

        _client = new AnthropicClient { ApiKey = _options.ApiKey };
    }

    public async Task<string?> SummariseAsync(string description, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var response = await _client.Messages.Create(
            new MessageCreateParams
            {
                Model = _options.Model,
                MaxTokens = MaxTokens,
                System = SystemPrompt,

                // Low effort suits a one-sentence summary, and keeps latency off
                // the customer's submit button. Note this leaves thinking enabled:
                // disabling it on Opus 5 risks internal tags leaking into the
                // visible reply, and low effort is the cheaper lever anyway.
                OutputConfig = new OutputConfig { Effort = Effort.Low },

                Messages = [new() { Role = Role.User, Content = description }]
            },
            cancellationToken);

        // Safety classifiers can decline a request; that arrives as a normal
        // 200 with an empty or partial body, so it must be checked before
        // reading content rather than after.
        if (response.StopReason == "refusal")
        {
            _logger.LogWarning(
                "Claude declined to summarise a ticket ({Category}). The ticket is unaffected.",
                response.StopDetails?.Category);

            return null;
        }

        var summary = string.Concat(
            response.Content
                .Select(block => block.Value)
                .OfType<TextBlock>()
                .Select(text => text.Text));

        return Normalise(summary);
    }

    /// <summary>
    /// Enforces the length ceiling the prompt only asks for. A model instruction
    /// is a request; this is the guarantee the rest of the system relies on.
    /// </summary>
    private string? Normalise(string summary)
    {
        var trimmed = summary.Trim();

        if (trimmed.Length == 0)
        {
            return null;
        }

        return trimmed.Length <= _options.MaxSummaryLength
            ? trimmed
            : trimmed[.._options.MaxSummaryLength].TrimEnd() + "…";
    }
}
