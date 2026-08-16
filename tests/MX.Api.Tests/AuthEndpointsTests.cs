using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MX.Application.Auth;
using MX.Application.Common;
using MX.Application.Tickets.Dtos;

namespace MX.Api.Tests;

/// <summary>
/// Sign-in and the admin-only edit rule.
///
/// The README's bonus requirement is that anyone may file a ticket while only a
/// signed-in admin may edit one, so these tests check both halves: that the open
/// door stays open and that the locked one is actually locked.
/// </summary>
public sealed class AuthEndpointsTests : IDisposable
{
    private readonly TicketApiFactory _factory = new();
    private readonly HttpClient _client;

    public AuthEndpointsTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<Guid> AnyTicketIdAsync()
    {
        var page = await _client.GetFromJsonAsync<PagedResult<TicketDto>>(
            "/api/tickets", TicketApiFactory.Json);

        return page!.Items[0].Id;
    }

    private static UpdateTicketRequest AnyEdit() => new(Status: "In Progress");

    // ------------------------------------------------------------------ login

    [Fact]
    public async Task Login_with_correct_credentials_describes_the_session()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(TicketApiFactory.AdminEmail, TicketApiFactory.Password));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(TicketApiFactory.Json);

        Assert.Equal("Admin", body!.Role);
        Assert.Equal(TicketApiFactory.AdminEmail, body.Email);
        Assert.True(body.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Login_issues_a_three_part_jwt()
    {
        var token = await _factory.LoginAsync(TicketApiFactory.AdminEmail);

        Assert.Equal(3, token.Split('.').Length);
    }

    // ------------------------------------------- keeping the token out of script

    [Fact]
    public async Task Login_puts_the_token_in_an_HttpOnly_cookie()
    {
        // The reason any of this is shaped the way it is: without HttpOnly, one
        // injected script on the page walks off with a working admin session.
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(TicketApiFactory.AdminEmail, TicketApiFactory.Password));

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));

        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_never_returns_the_token_in_the_body()
    {
        // The complement of the test above. HttpOnly is worthless if the same
        // token is also handed to the page in JSON for it to stash somewhere.
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(TicketApiFactory.AdminEmail, TicketApiFactory.Password));

        var token = TicketApiFactory.TokenFromCookie(response);
        var raw = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(token, raw, StringComparison.Ordinal);
        Assert.DoesNotContain("accessToken", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_session_cookie_alone_authenticates_an_edit()
    {
        // A browser sends no Authorization header. If this fails, the frontend is
        // signed out no matter what the login screen appeared to do.
        using var admin = await _factory.CreateAuthenticatedClientAsync(TicketApiFactory.AdminEmail);

        Assert.Null(admin.DefaultRequestHeaders.Authorization);

        var response = await admin.PutAsJsonAsync($"/api/tickets/{await AnyTicketIdAsync()}", AnyEdit());

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_bearer_token_still_authenticates_for_non_browser_callers()
    {
        using var admin = await _factory.CreateBearerClientAsync(TicketApiFactory.AdminEmail);

        var response = await admin.PutAsJsonAsync($"/api/tickets/{await AnyTicketIdAsync()}", AnyEdit());

        response.EnsureSuccessStatusCode();
    }

    // ----------------------------------------------------- describing the session

    [Fact]
    public async Task Me_reports_who_the_cookie_belongs_to()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(TicketApiFactory.AdminEmail);

        var body = await admin.GetFromJsonAsync<LoginResponse>("/api/auth/me", TicketApiFactory.Json);

        Assert.Equal(TicketApiFactory.AdminEmail, body!.Email);
        Assert.Equal("Admin", body.Role);
        Assert.True(body.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Me_without_a_session_is_401()
    {
        // The frontend restores a session by calling this on boot, so the
        // signed-out answer has to be the ordinary 401 it knows to expect.
        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ------------------------------------------------------------- signing out

    [Fact]
    public async Task Logout_clears_the_session()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(TicketApiFactory.AdminEmail);

        var logout = await admin.PostAsync("/api/auth/logout", content: null);
        logout.EnsureSuccessStatusCode();

        // The page cannot delete an HttpOnly cookie itself, so if the server does
        // not expire it here, "sign out" only hides the UI.
        var after = await admin.PutAsJsonAsync($"/api/tickets/{await AnyTicketIdAsync()}", AnyEdit());

        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task Logout_without_a_session_still_succeeds()
    {
        var response = await _client.PostAsync("/api/auth/logout", content: null);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Login_never_echoes_the_password_or_its_hash()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(TicketApiFactory.AdminEmail, TicketApiFactory.Password));

        var raw = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(TicketApiFactory.Password, raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PasswordHash", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_matches_the_email_case_insensitively()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(TicketApiFactory.AdminEmail.ToUpperInvariant(), TicketApiFactory.Password));

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Login_with_a_wrong_password_is_rejected()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(TicketApiFactory.AdminEmail, "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_an_unknown_account_is_rejected_identically()
    {
        // Identical status and body for "no such user" and "wrong password", so
        // the endpoint cannot be used to discover which accounts exist.
        var unknown = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("nobody@test.local", TicketApiFactory.Password));

        var wrongPassword = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(TicketApiFactory.AdminEmail, "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(wrongPassword.StatusCode, unknown.StatusCode);

        // Everything except traceId, which is a per-request correlation id and
        // carries nothing about the account.
        Assert.Equal(await DescribeProblemAsync(wrongPassword), await DescribeProblemAsync(unknown));
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

    [Theory]
    [InlineData("", "password")]
    [InlineData("admin@test.local", "")]
    public async Task Login_with_missing_fields_is_a_validation_error(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------- protecting edits

    [Fact]
    public async Task Editing_without_a_token_is_401()
    {
        var response = await _client.PutAsJsonAsync($"/api/tickets/{await AnyTicketIdAsync()}", AnyEdit());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Editing_with_a_signed_in_non_admin_is_403()
    {
        // Authenticated but not permitted — the distinction the Admin policy exists for.
        using var viewer = await _factory.CreateAuthenticatedClientAsync(TicketApiFactory.EditorlessEmail);

        var response = await viewer.PutAsJsonAsync($"/api/tickets/{await AnyTicketIdAsync()}", AnyEdit());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Editing_as_an_admin_succeeds()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(TicketApiFactory.AdminEmail);

        var response = await admin.PutAsJsonAsync($"/api/tickets/{await AnyTicketIdAsync()}", AnyEdit());

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Editing_with_a_garbage_token_is_401()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.token");

        var response = await client.PutAsJsonAsync($"/api/tickets/{await AnyTicketIdAsync()}", AnyEdit());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Editing_with_a_token_signed_by_the_wrong_key_is_401()
    {
        // The single most important check here: a well-formed JWT claiming the
        // Admin role is worthless unless this API signed it.
        var forged = ForgeAdminToken("a-different-signing-key-that-is-long-enough-32");

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forged);

        var response = await client.PutAsJsonAsync($"/api/tickets/{await AnyTicketIdAsync()}", AnyEdit());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_rejected_edit_changes_nothing_on_disk()
    {
        var before = await File.ReadAllTextAsync(_factory.DataFilePath);

        await _client.PutAsJsonAsync($"/api/tickets/{await AnyTicketIdAsync()}", AnyEdit());

        Assert.Equal(before, await File.ReadAllTextAsync(_factory.DataFilePath));
    }

    // --------------------------------------------------- staying open to all

    [Fact]
    public async Task Anyone_may_still_file_a_ticket_without_signing_in()
    {
        // Guards against the obvious over-correction: locking down the whole
        // controller and shutting customers out of the thing it exists for.
        var response = await _client.PostAsJsonAsync("/api/tickets",
            new CreateTicketRequest("Anonymous Customer", "anon@example.com", "My toaster is sentient."));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Anyone_may_still_read_tickets_without_signing_in()
    {
        var response = await _client.GetAsync("/api/tickets");

        response.EnsureSuccessStatusCode();
    }

    private static string ForgeAdminToken(string signingKey)
    {
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(signingKey));

        var descriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Issuer = "mx-support-api",
            Audience = "mx-support-frontend",
            Expires = DateTime.UtcNow.AddMinutes(30),
            Subject = new System.Security.Claims.ClaimsIdentity(
            [
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin")
            ]),
            SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256)
        };

        return new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler().CreateToken(descriptor);
    }
}
