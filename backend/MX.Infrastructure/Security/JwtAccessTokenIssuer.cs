using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MX.Application.Abstractions;
using MX.Domain.Users;
using MX.Infrastructure.Configuration;

namespace MX.Infrastructure.Security;

/// <summary>
/// Issues HS256-signed JWTs.
///
/// The token carries the user's email and role and nothing else. Anything placed
/// in a JWT is readable by whoever holds it — the signature proves the payload
/// was not altered, it does not conceal it — so the payload stays limited to what
/// authorization actually needs.
/// </summary>
public sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider)
    : IAccessTokenIssuer
{
    private readonly JwtOptions _options = options.Value;

    public AccessToken Issue(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var issuedAt = timeProvider.GetUtcNow();
        var expiresAt = issuedAt.AddMinutes(_options.ExpiryMinutes);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),

                // A unique token id, so individual tokens could be denylisted
                // later without invalidating everyone's session.
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ]),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);

        return new AccessToken(token, expiresAt);
    }
}
