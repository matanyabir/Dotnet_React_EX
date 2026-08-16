using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using MX.Api.Authentication;
using MX.Application.Auth;

namespace MX.Api.Endpoints;

/// <summary>
/// Sign-in, sign-out, and "who am I".
///
/// Sign-in is anonymous by necessity — this is where a caller goes to stop being
/// anonymous. The token it produces never appears in a response body: it is set
/// as an <c>HttpOnly</c> cookie (see <see cref="SessionCookie"/>), which is why
/// <c>/me</c> exists at all. The frontend cannot read its own token to learn who
/// it is signed in as, so it asks.
/// </summary>
internal static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithSummary("Exchanges email and password for a session cookie.")
            .AllowAnonymous();

        group.MapPost("/logout", Logout)
            .WithName("Logout")
            .WithSummary("Clears the session cookie.")
            // Anonymous on purpose: signing out has to work when the cookie has
            // already expired, which is exactly when it would fail a check.
            .AllowAnonymous();

        group.MapGet("/me", Me)
            .WithName("CurrentSession")
            .WithSummary("Describes the session the caller's cookie carries.")
            .RequireAuthorization();

        return group;
    }

    private static async Task<IResult> LoginAsync(
        HttpContext context,
        IAuthService auth,
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(request, cancellationToken);

        return result.ToHttpResult(signIn =>
        {
            SessionCookie.Append(context, signIn.AccessToken, signIn.ExpiresAt);

            return Results.Ok(new LoginResponse(signIn.ExpiresAt, signIn.Email, signIn.Role));
        });
    }

    private static IResult Logout(HttpContext context)
    {
        SessionCookie.Delete(context);

        return Results.NoContent();
    }

    /// <summary>
    /// Rebuilt from the validated token's claims rather than from storage: if the
    /// caller got this far, authentication already proved the claims are ones this
    /// API issued and has not expired.
    /// </summary>
    private static IResult Me(HttpContext context)
    {
        var user = context.User;

        var email = user.FindFirstValue(JwtRegisteredClaimNames.Email)
                    ?? user.FindFirstValue(ClaimTypes.Email)
                    ?? string.Empty;

        var role = user.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        return Results.Ok(new LoginResponse(ExpiryOf(user), email, role));
    }

    /// <summary>
    /// The token's own expiry, as a timestamp the client can compare against.
    ///
    /// Both spellings are checked because the bearer handler renames some inbound
    /// claims to their long-form URIs and leaves others alone; which bucket
    /// <c>exp</c> falls into is a detail of that map, not something worth
    /// depending on. Falling back to "now" would be a lie, so an absent claim
    /// reports the moment the token would have to be re-checked anyway.
    /// </summary>
    private static DateTimeOffset ExpiryOf(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(JwtRegisteredClaimNames.Exp)
                  ?? user.FindFirstValue(ClaimTypes.Expiration);

        return long.TryParse(raw, out var unixSeconds)
            ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
            : DateTimeOffset.UtcNow;
    }
}
