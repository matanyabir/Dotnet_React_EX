using Microsoft.Extensions.DependencyInjection;
using MX.Application.Auth;
using MX.Application.Tickets;

namespace MX.Application;

/// <summary>
/// Each layer registers its own services, so the composition root in Program.cs
/// stays a list of layers rather than a list of every class in the solution.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<IAuthService, AuthService>();

        // Injected rather than called statically, so tests can freeze time.
        services.TryAddSingletonTimeProvider();

        return services;
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (services.All(d => d.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
