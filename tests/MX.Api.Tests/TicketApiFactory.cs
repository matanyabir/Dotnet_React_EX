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

    /// <summary>Signs in and returns the access token.</summary>
    public async Task<string> LoginAsync(string email, string password = Password)
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
        return body!.AccessToken;
    }

    /// <summary>A client whose requests carry a bearer token for the given account.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string email)
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
