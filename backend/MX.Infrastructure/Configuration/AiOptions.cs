namespace MX.Infrastructure.Configuration;

/// <summary>Which summary implementation to use.</summary>
public enum AiProvider
{
    /// <summary>Produce no summary at all. The feature is off.</summary>
    None = 0,

    /// <summary>
    /// Deterministic local truncation. No network, no key, no cost — the default,
    /// so the exercise demonstrates the feature immediately after a clone.
    /// </summary>
    Stub,

    /// <summary>Call Claude to write a real summary.</summary>
    Claude
}

/// <summary>
/// AI summary settings, bound from the "Ai" section.
///
/// <see cref="Provider"/> is the whole switch between local and real
/// summarisation — the point of putting all three behind one port.
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public AiProvider Provider { get; set; } = AiProvider.Stub;

    /// <summary>
    /// Anthropic API key. Belongs in user-secrets or an environment variable,
    /// never in a committed file:
    ///   dotnet user-secrets set "Ai:ApiKey" "sk-ant-..."
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>The Claude model to summarise with.</summary>
    public string Model { get; set; } = "claude-opus-5";

    /// <summary>
    /// How long to wait before giving up on a summary. Short on purpose: a
    /// customer filing a ticket must not wait on an AI provider, and the
    /// resilience decorator turns an expiry into "no summary" rather than an error.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Upper bound on the summary length, enforced after generation. The prompt
    /// asks for one short sentence; this is the guarantee, not the request.
    /// </summary>
    public int MaxSummaryLength { get; set; } = 200;
}
