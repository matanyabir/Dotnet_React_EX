using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using MX.Application.Auth;
using MX.Application.Tickets.Dtos;

namespace MX.Api.Tests;

/// <summary>
/// The cap on repeated sign-in attempts.
///
/// A correct password is the only thing that should open the door, and without a
/// limit "correct" is reachable by patience alone. These tests fix the two halves
/// of that: attempts past the cap are refused, and the refusal cannot be stepped
/// around — including by a caller who then supplies the right password.
///
/// The limit is configured tightly here rather than borrowing the shipped
/// default, so the test states the number it depends on instead of breaking the
/// day someone tunes appsettings.json.
/// </summary>
public sealed class LoginRateLimitTests : IDisposable
{
    private const int Limit = 3;

    private readonly TicketApiFactory _factory = new();
    private readonly WebApplicationFactory<Program> _throttled;
    private readonly HttpClient _client;

    public LoginRateLimitTests()
    {
        _throttled = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth:LoginRateLimit:PermitLimit", $"{Limit}");

            // Long enough that no test can outlast the window and see it reset
            // mid-run, which would make these pass or fail on timing.
            builder.UseSetting("Auth:LoginRateLimit:WindowSeconds", "600");
        });

        _client = _throttled.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _throttled.Dispose();
        _factory.Dispose();
    }

    private Task<HttpResponseMessage> AttemptAsync(string password = "wrong-password") =>
        _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(TicketApiFactory.AdminEmail, password));

    /// <summary>Spends the whole allowance, leaving the next attempt over the line.</summary>
    private async Task ExhaustTheAllowanceAsync()
    {
        for (var attempt = 0; attempt < Limit; attempt++)
        {
            using var response = await AttemptAsync();

            // If any of these were already throttled the limit is tighter than
            // configured, and every assertion after it would mean nothing.
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Attempts_within_the_allowance_are_answered_normally()
    {
        // The complement of every test below: a limit that throttles the honest
        // second try is a broken login screen, not a protected one.
        await ExhaustTheAllowanceAsync();
    }

    [Fact]
    public async Task Attempts_past_the_allowance_are_rejected_with_429()
    {
        await ExhaustTheAllowanceAsync();

        using var response = await AttemptAsync();

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task A_correct_password_does_not_step_past_the_limit()
    {
        // The one that matters. Checking credentials before checking the limit
        // would leave the guessing loop intact and merely slow down the losers.
        await ExhaustTheAllowanceAsync();

        using var response = await AttemptAsync(TicketApiFactory.Password);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task A_throttled_attempt_says_when_to_try_again()
    {
        await ExhaustTheAllowanceAsync();

        using var response = await AttemptAsync();

        var retryAfter = response.Headers.RetryAfter?.Delta;

        Assert.NotNull(retryAfter);
        Assert.InRange(retryAfter.Value, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(600));
    }

    [Fact]
    public async Task A_throttled_attempt_comes_back_as_problem_details()
    {
        // The frontend reads every failure out of a ProblemDetails body. A 429
        // in any other shape reaches the user as "the request failed (429)".
        await ExhaustTheAllowanceAsync();

        using var response = await AttemptAsync();

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("Too many sign-in attempts", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_throttled_attempt_reveals_nothing_about_the_account()
    {
        // The limit counts addresses, not accounts, so it must answer a real
        // email and an invented one identically — otherwise it becomes the
        // account-enumeration oracle that AuthService is written to deny.
        await ExhaustTheAllowanceAsync();

        using var known = await AttemptAsync();
        using var unknown = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("nobody@test.local", "wrong-password"));

        Assert.Equal(HttpStatusCode.TooManyRequests, unknown.StatusCode);

        // Everything except traceId, which is a per-request correlation id and
        // carries nothing about the account.
        Assert.Equal(await DescribeProblemAsync(known), await DescribeProblemAsync(unknown));
    }

    /// <summary>The parts of a ProblemDetails body that could leak information.</summary>
    private static async Task<string> DescribeProblemAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        static string Read(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) ? value.ToString() : "<absent>";

        return string.Join('|', Read(root, "type"), Read(root, "title"),
            Read(root, "status"), Read(root, "detail"));
    }

    [Fact]
    public async Task The_limit_reaches_no_further_than_sign_in()
    {
        // Guards the over-correction: throttling the whole API would mean a
        // spent login allowance also stops customers filing tickets.
        await ExhaustTheAllowanceAsync();
        using var throttled = await AttemptAsync();

        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);

        using var filed = await _client.PostAsJsonAsync("/api/tickets",
            new CreateTicketRequest("Anonymous Customer", "anon@example.com", "My toaster is sentient."));

        Assert.Equal(HttpStatusCode.Created, filed.StatusCode);

        using var read = await _client.GetAsync("/api/tickets");

        read.EnsureSuccessStatusCode();
    }
}
