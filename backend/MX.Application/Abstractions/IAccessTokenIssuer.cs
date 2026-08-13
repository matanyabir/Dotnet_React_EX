using MX.Domain.Users;

namespace MX.Application.Abstractions;

/// <summary>A signed token and the moment it stops being valid.</summary>
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>
/// Mints access tokens for authenticated users.
///
/// The application layer knows it hands out a token; it does not know the token
/// is a JWT, which is why this returns a string rather than a JWT type.
/// </summary>
public interface IAccessTokenIssuer
{
    AccessToken Issue(User user);
}
