namespace MX.Application.Auth;

/// <summary>Credentials from the login screen.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>
/// A verified sign-in, including the token itself.
///
/// Internal to the server: the API layer takes the token from here and puts it
/// in an <c>HttpOnly</c> cookie, and it is never serialised to the client. Only
/// <see cref="LoginResponse"/> crosses the wire.
/// </summary>
public sealed record SignInResult(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string Email,
    string Role);

/// <summary>
/// What a successful sign-in returns.
///
/// Deliberately excludes anything sensitive: no password hash, no internal id,
/// and no access token — that travels in a cookie the browser will not hand to
/// script. What is left describes the session so the UI can render it.
/// <see cref="ExpiresAt"/> lets the frontend stop offering admin controls before
/// the cookie lapses, rather than discovering it through a failed save.
/// </summary>
public sealed record LoginResponse(
    DateTimeOffset ExpiresAt,
    string Email,
    string Role);
