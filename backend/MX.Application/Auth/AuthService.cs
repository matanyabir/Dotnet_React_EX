using MX.Application.Abstractions;
using MX.Application.Common;

namespace MX.Application.Auth;

/// <summary>Signs users in.</summary>
public interface IAuthService
{
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Verifies credentials and issues an access token.
///
/// Two security properties are deliberate rather than incidental:
///
/// 1. An unknown email and a wrong password produce the identical response, so
///    the endpoint cannot be used to enumerate which accounts exist.
/// 2. A miss still performs one hash verification against a throwaway hash, so
///    the two cases also take comparable time — otherwise the *duration* of the
///    reply would leak what the message carefully does not.
/// </summary>
public sealed class AuthService : IAuthService
{
    private const string RejectionMessage = "Email address or password is incorrect.";

    private readonly IUserStore _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccessTokenIssuer _tokenIssuer;
    private readonly Lazy<string> _decoyHash;

    public AuthService(IUserStore users, IPasswordHasher passwordHasher, IAccessTokenIssuer tokenIssuer)
    {
        ArgumentNullException.ThrowIfNull(users);
        ArgumentNullException.ThrowIfNull(passwordHasher);
        ArgumentNullException.ThrowIfNull(tokenIssuer);

        _users = users;
        _passwordHasher = passwordHasher;
        _tokenIssuer = tokenIssuer;

        // Built once, on the first failed lookup, and never matches anything.
        _decoyHash = new Lazy<string>(() => passwordHasher.Hash(Guid.NewGuid().ToString()));
    }

    public async Task<Result<LoginResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Result<LoginResponse>.Invalid("Email address and password are both required.");
        }

        var user = await _users.FindByEmailAsync(request.Email, cancellationToken).ConfigureAwait(false);

        if (user is null)
        {
            // Burn the same work a real verification would, then reject.
            _ = _passwordHasher.Verify(request.Password, _decoyHash.Value);
            return Result<LoginResponse>.Unauthorized(RejectionMessage);
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Result<LoginResponse>.Unauthorized(RejectionMessage);
        }

        var token = _tokenIssuer.Issue(user);

        return Result<LoginResponse>.Success(
            new LoginResponse(token.Token, token.ExpiresAt, user.Email, user.Role));
    }
}
