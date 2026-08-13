using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MX.Application.Abstractions;
using MX.Infrastructure.Ai;
using MX.Infrastructure.Configuration;

namespace MX.Infrastructure.Tests;

/// <summary>
/// The stub provider and, more importantly, the resilience decorator — the
/// component that makes ISummaryGenerator's "must not throw, must not hang"
/// contract true rather than merely documented.
/// </summary>
public class SummaryGeneratorTests
{
    private static StubSummaryGenerator Stub(int maxLength = 200) =>
        new(Options.Create(new AiOptions { MaxSummaryLength = maxLength }));

    private static ResilientSummaryGenerator Resilient(ISummaryGenerator inner, int timeoutMs = 200) =>
        new(inner, TimeSpan.FromMilliseconds(timeoutMs), NullLogger<ResilientSummaryGenerator>.Instance);

    // ------------------------------------------------------------------- stub

    [Fact]
    public async Task Stub_takes_the_first_sentence()
    {
        var summary = await Stub().SummariseAsync(
            "The washing machine will not drain. It started on Tuesday. I have tried everything.");

        Assert.Equal("The washing machine will not drain.", summary);
    }

    [Fact]
    public async Task Stub_keeps_a_single_sentence_whole()
    {
        var summary = await Stub().SummariseAsync("My laptop overheats and shuts down.");

        Assert.Equal("My laptop overheats and shuts down.", summary);
    }

    [Fact]
    public async Task Stub_handles_a_description_with_no_sentence_ending()
    {
        var summary = await Stub().SummariseAsync("printer jammed again");

        Assert.Equal("printer jammed again", summary);
    }

    [Fact]
    public async Task Stub_truncates_at_a_word_boundary()
    {
        var summary = await Stub(maxLength: 20).SummariseAsync(
            "The dishwasher floods the entire kitchen every single time it runs");

        Assert.NotNull(summary);
        Assert.True(summary.Length <= 21, $"Expected <= 21 chars, got {summary.Length}: '{summary}'");
        Assert.EndsWith("…", summary, StringComparison.Ordinal);

        // A cut mid-word would leave a fragment; the boundary rule prevents it.
        Assert.DoesNotContain("dishwash…", summary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Stub_returns_null_for_blank_input(string blank)
    {
        Assert.Null(await Stub().SummariseAsync(blank));
    }

    [Fact]
    public async Task Stub_is_deterministic()
    {
        // Why it doubles as a test double: same input, same summary, every run.
        const string Description = "The air conditioner blows warm air. It is very hot.";

        Assert.Equal(
            await Stub().SummariseAsync(Description),
            await Stub().SummariseAsync(Description));
    }

    // -------------------------------------------------------------- decorator

    [Fact]
    public async Task Resilient_passes_a_successful_summary_straight_through()
    {
        var summary = await Resilient(new FakeGenerator("Printer ablaze.")).SummariseAsync("anything");

        Assert.Equal("Printer ablaze.", summary);
    }

    [Fact]
    public async Task Resilient_turns_a_thrown_exception_into_no_summary()
    {
        var summary = await Resilient(new ThrowingGenerator(new HttpRequestException("network down")))
            .SummariseAsync("anything");

        Assert.Null(summary);
    }

    [Theory]
    [MemberData(nameof(ProviderFailures))]
    public async Task Resilient_absorbs_every_kind_of_provider_failure(Exception failure)
    {
        // Auth, quota, malformed response, bug in the provider — all have the
        // same correct outcome: no summary, and the ticket is unaffected.
        Assert.Null(await Resilient(new ThrowingGenerator(failure)).SummariseAsync("anything"));
    }

    public static TheoryData<Exception> ProviderFailures() =>
    [
        new HttpRequestException("connection refused"),
        new InvalidOperationException("malformed response"),
        new UnauthorizedAccessException("bad API key"),
        new TimeoutException("upstream timeout"),
        new NullReferenceException("bug in the provider")
    ];

    [Fact]
    public async Task Resilient_gives_up_on_a_hanging_provider()
    {
        var summary = await Resilient(new HangingGenerator(), timeoutMs: 100).SummariseAsync("anything");

        Assert.Null(summary);
    }

    [Fact]
    public async Task Resilient_returns_promptly_rather_than_waiting_out_the_hang()
    {
        // The point of the timeout: a customer's submit button must not wait on
        // an unresponsive AI provider.
        var started = DateTimeOffset.UtcNow;

        await Resilient(new HangingGenerator(), timeoutMs: 100).SummariseAsync("anything");

        Assert.True(
            DateTimeOffset.UtcNow - started < TimeSpan.FromSeconds(5),
            "The decorator should abandon a hanging provider, not wait for it.");
    }

    [Fact]
    public async Task Resilient_still_propagates_cancellation_from_the_caller()
    {
        // The caller giving up is not a summary failure to swallow. Abandoned
        // work should stop rather than run on.
        using var caller = new CancellationTokenSource();
        await caller.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Resilient(new HangingGenerator(), timeoutMs: 30_000)
                .SummariseAsync("anything", caller.Token));
    }

    [Fact]
    public async Task Resilient_composes_with_the_stub()
    {
        // The shape actually registered in DI: a real provider behind the guard.
        var summary = await Resilient(Stub()).SummariseAsync("The kettle will not boil. It just clicks.");

        Assert.Equal("The kettle will not boil.", summary);
    }

    [Fact]
    public async Task Null_provider_yields_no_summary_without_failing()
    {
        // "Switched off" must be indistinguishable from "nothing useful to say".
        Assert.Null(await Resilient(new NullSummaryGenerator()).SummariseAsync("anything"));
    }

    // ------------------------------------------------------------------ fakes

    private sealed class FakeGenerator(string? summary) : ISummaryGenerator
    {
        public Task<string?> SummariseAsync(string description, CancellationToken cancellationToken = default) =>
            Task.FromResult(summary);
    }

    private sealed class ThrowingGenerator(Exception failure) : ISummaryGenerator
    {
        public Task<string?> SummariseAsync(string description, CancellationToken cancellationToken = default) =>
            throw failure;
    }

    /// <summary>A provider that never answers until cancelled.</summary>
    private sealed class HangingGenerator : ISummaryGenerator
    {
        public async Task<string?> SummariseAsync(
            string description,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return null;
        }
    }
}
