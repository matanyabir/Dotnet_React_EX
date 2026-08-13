using MX.Application.Auth;

namespace MX.Api.Endpoints;

/// <summary>
/// Sign-in. Anonymous by necessity — this is where a caller goes to stop being
/// anonymous.
/// </summary>
internal static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithSummary("Exchanges email and password for an access token.")
            .AllowAnonymous();

        return group;
    }

    private static async Task<IResult> LoginAsync(
        IAuthService auth,
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}
