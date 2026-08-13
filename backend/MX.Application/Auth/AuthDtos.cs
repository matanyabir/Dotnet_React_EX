namespace MX.Application.Auth;

/// <summary>Credentials from the login screen.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>
/// What a successful sign-in returns.
///
/// Deliberately excludes anything sensitive: no password hash, no internal id.
/// <see cref="ExpiresAt"/> lets the frontend drop a stale token before making a
/// request that would fail, rather than discovering it through a 401.
/// </summary>
public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string Email,
    string Role);
