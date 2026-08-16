using System.ComponentModel.DataAnnotations;

namespace MX.Infrastructure.Configuration;

/// <summary>
/// Token signing and lifetime, bound from "Auth:Jwt".
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Auth:Jwt";

    /// <summary>
    /// HMAC signing secret. Anyone holding this can mint admin tokens, so it
    /// belongs in user-secrets or an environment variable outside development.
    /// </summary>
    [Required]
    [MinLength(32, ErrorMessage = "The JWT signing key must be at least 32 characters (256 bits) for HS256.")]
    public string SigningKey { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = "mx-support-api";

    [Required]
    public string Audience { get; set; } = "mx-support-frontend";

    /// <summary>
    /// Short by design. A stolen token stays useful until it expires, and there
    /// is no revocation list here to cut it short.
    /// </summary>
    [Range(1, 24 * 60)]
    public int ExpiryMinutes { get; set; } = 60;
}

/// <summary>
/// How hard one client may hammer the sign-in endpoint, bound from
/// "Auth:LoginRateLimit".
///
/// Sign-in is the one route where a caller can spend the server's CPU without
/// having proved anything first — a wrong password still costs a full
/// 600,000-iteration PBKDF2 verification — and it is the one route where
/// repetition alone eventually succeeds. Both problems have the same answer:
/// cap how often a given client may try.
/// </summary>
public sealed class LoginRateLimitOptions
{
    public const string SectionName = "Auth:LoginRateLimit";

    /// <summary>
    /// Attempts allowed per client per <see cref="WindowSeconds"/>. Sized to sit
    /// well above a person who has forgotten which password they used and well
    /// below anything that could work through a word list.
    /// </summary>
    [Range(1, 10_000)]
    public int PermitLimit { get; set; } = 10;

    [Range(1, 24 * 60 * 60)]
    public int WindowSeconds { get; set; } = 300;

    public TimeSpan Window => TimeSpan.FromSeconds(WindowSeconds);
}

/// <summary>One sign-in account, bound from an entry in "Auth:Users".</summary>
public sealed class UserOptions
{
    [Required]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// A <c>Pbkdf2PasswordHasher</c> hash — never a plaintext password. Storing
    /// the hash means a leaked config file does not immediately yield a usable
    /// credential.
    /// </summary>
    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;
}

/// <summary>The accounts that may sign in, bound from "Auth".</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public IList<UserOptions> Users { get; set; } = [];
}
