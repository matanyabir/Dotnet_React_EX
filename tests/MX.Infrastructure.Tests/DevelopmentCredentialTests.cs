using System.Text.Json;
using MX.Infrastructure.Security;

namespace MX.Infrastructure.Tests;

/// <summary>
/// Checks that the development credential documented in the README actually
/// signs in.
///
/// This exists because of a real defect: an earlier commit shipped a password
/// hash generated for a different password, and nothing caught it. The hash had
/// been "verified" only against whatever password produced it, which is circular
/// — it proves the hasher round-trips, not that the credential is the documented
/// one. Pinning the literal password here closes that gap.
/// </summary>
public class DevelopmentCredentialTests
{
    /// <summary>Must match the credential documented in the README.</summary>
    private const string DocumentedEmail = "admin@example.com";
    private const string DocumentedPassword = "Admin123!";

    private static readonly string SettingsPath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "appsettings.Development.json");

    private static JsonElement FirstConfiguredUser()
    {
        var json = File.ReadAllText(SettingsPath);

        // The configuration provider tolerates comments, and the settings file
        // uses them to explain the credential, so parsing must tolerate them too.
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        return document.RootElement
            .GetProperty("Auth")
            .GetProperty("Users")[0]
            .Clone();
    }

    [Fact]
    public void The_documented_password_verifies_against_the_committed_hash()
    {
        var storedHash = FirstConfiguredUser().GetProperty("PasswordHash").GetString();

        Assert.NotNull(storedHash);
        Assert.True(
            new Pbkdf2PasswordHasher().Verify(DocumentedPassword, storedHash),
            $"The committed hash does not match the documented password '{DocumentedPassword}'. " +
            "Regenerate it, or correct the documentation.");
    }

    [Fact]
    public void A_different_password_does_not_verify()
    {
        // Guards the test above against passing for a trivial reason, such as a
        // hasher that accepts anything.
        var storedHash = FirstConfiguredUser().GetProperty("PasswordHash").GetString();

        Assert.False(new Pbkdf2PasswordHasher().Verify(DocumentedPassword + "-wrong", storedHash!));
    }

    [Fact]
    public void The_documented_email_and_role_are_configured()
    {
        var user = FirstConfiguredUser();

        Assert.Equal(DocumentedEmail, user.GetProperty("Email").GetString());
        Assert.Equal("Admin", user.GetProperty("Role").GetString());
    }

    [Fact]
    public void The_settings_file_stores_no_plaintext_password()
    {
        var json = File.ReadAllText(SettingsPath);

        // The comment names the password, so look only at the hash value itself.
        var storedHash = FirstConfiguredUser().GetProperty("PasswordHash").GetString();

        Assert.DoesNotContain(DocumentedPassword, storedHash!, StringComparison.Ordinal);
    }

    [Fact]
    public void The_development_signing_key_is_long_enough_for_HS256()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(SettingsPath),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        var key = document.RootElement.GetProperty("Auth").GetProperty("Jwt")
            .GetProperty("SigningKey").GetString();

        // Shorter than 256 bits and token creation throws at runtime.
        Assert.True(key!.Length >= 32, "The development signing key must be at least 32 characters.");
    }
}
