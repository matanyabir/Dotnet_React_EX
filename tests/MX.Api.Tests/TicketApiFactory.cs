using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MX.Application.Auth;

namespace MX.Api.Tests;

/// <summary>
/// Boots the real API pipeline over a throwaway copy of the dataset.
///
/// xUnit constructs a fresh test-class instance per test, so a factory created in
/// a test class's constructor gives every test its own file and its own singleton
/// repository. Tests can therefore write freely without ordering constraints or
/// leaking state into one another — and the committed dataset is never touched.
/// </summary>
public sealed class TicketApiFactory : WebApplicationFactory<Program>
{
    private static readonly string PristineDataset =
        Path.Combine(AppContext.BaseDirectory, "TestData", "dataset.json");

    public const string AdminEmail = "admin@test.local";
    public const string EditorlessEmail = "viewer@test.local";
    public const string Password = "test-password";

    /// <summary>
    /// The same password hashed at 1,000 PBKDF2 iterations instead of the
    /// production 600,000. Verification reads the cost out of the stored hash, so
    /// this exercises the real hasher while keeping a login off the critical path
    /// of every test.
    /// </summary>
    private const string TestPasswordHash =
        "1000.WmUWdy1Xfz5KvZ3X5spcbQ==.8tOLkH5pZvjmva2HSHa1hORkARyAAZ3LSVq0lIES9UE=";

    private const string TestSigningKey = "integration-test-signing-key-0123456789abcdef";

    public TicketApiFactory()
    {
        DataFilePath = Path.Combine(Path.GetTempPath(), $"mx-api-{Guid.NewGuid():N}.json");
        File.Copy(PristineDataset, DataFilePath);
    }

    /// <summary>The temp dataset this instance reads and writes.</summary>
    public string DataFilePath { get; }

    /// <summary>
    /// Matches the API's own serializer settings, so tests parse responses the way
    /// a real client would rather than by hand-massaging property names.
    /// </summary>
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // An absolute path, so the result does not depend on the test host's
        // content root differing from the API project's.
        builder.UseSetting("Storage:DataFilePath", DataFilePath);
        builder.UseEnvironment("Testing");

        // The Testing environment loads no appsettings.Development.json, so auth
        // is configured here in full. That also proves the app takes its accounts
        // from configuration rather than anything hardcoded.
        builder.UseSetting("Auth:Jwt:SigningKey", TestSigningKey);

        builder.UseSetting("Auth:Users:0:Email", AdminEmail);
        builder.UseSetting("Auth:Users:0:PasswordHash", TestPasswordHash);
        builder.UseSetting("Auth:Users:0:Role", "Admin");

        // A second, non-admin account: the only way to tell "not signed in" (401)
        // apart from "signed in but not permitted" (403).
        builder.UseSetting("Auth:Users:1:Email", EditorlessEmail);
        builder.UseSetting("Auth:Users:1:PasswordHash", TestPasswordHash);
        builder.UseSetting("Auth:Users:1:Role", "Viewer");
    }

    /// <summary>
    /// Signs in and returns the access token the session cookie carries.
    ///
    /// The token is no longer in the response body — it is set as an HttpOnly
    /// cookie — so it is read back out of the Set-Cookie header here. That is a
    /// thing only a test does: it exists to assert on the token's shape, not
    /// because any client needs it.
    /// </summary>
    public async Task<string> LoginAsync(string email, string password = Password)
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();

        return TokenFromCookie(response);
    }

    /// <summary>The session cookie's value, or a failed assertion explaining its absence.</summary>
    public static string TokenFromCookie(HttpResponseMessage response)
    {
        var cookie = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(value =>
                SessionCookieNames.Any(name => value.StartsWith($"{name}=", StringComparison.Ordinal)))
            : null;

        Assert.NotNull(cookie);

        // "name=value; Path=/; ..." — everything up to the first semicolon.
        var value = cookie.Split(';', 2)[0];
        return value[(value.IndexOf('=') + 1)..];
    }

    /// <summary>Both spellings the API uses, depending on whether the request was HTTPS.</summary>
    public static readonly string[] SessionCookieNames = ["__Host-mx_session", "mx_session"];

    /// <summary>
    /// A client signed in as the given account.
    ///
    /// The cookie handler that <see cref="WebApplicationFactory{TEntryPoint}"/>
    /// installs by default keeps the session cookie from the login response and
    /// replays it, which is exactly what a browser does — so these tests exercise
    /// the path the frontend actually takes rather than a header no browser sends.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string email)
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        response.EnsureSuccessStatusCode();

        return client;
    }

    /// <summary>A client that authenticates by header instead, as a non-browser caller would.</summary>
    public async Task<HttpClient> CreateBearerClientAsync(string email)
    {
        var token = await LoginAsync(email);

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && File.Exists(DataFilePath))
        {
            File.Delete(DataFilePath);
        }
    }
}
