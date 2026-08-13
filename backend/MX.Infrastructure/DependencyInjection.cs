using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MX.Application.Abstractions;
using MX.Domain.Common;
using MX.Infrastructure.Ai;
using MX.Infrastructure.Configuration;
using MX.Infrastructure.Events;
using MX.Infrastructure.Persistence;

namespace MX.Infrastructure;

/// <summary>
/// Binds the ports declared in MX.Application to their concrete adapters. This is
/// the only place in the solution that knows tickets live in a JSON file.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        // Singleton on purpose: the repository caches the dataset in memory and
        // guards writes with a semaphore. Per-request instances would each hold a
        // private cache and a private lock, which protects nothing.
        services.AddSingleton<ITicketRepository>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<StorageOptions>>().Value;
            return new JsonTicketRepository(Resolve(options.DataFilePath, contentRootPath));
        });

        services.AddSingleton<ISummaryGenerator, NullSummaryGenerator>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        return services;
    }

    /// <summary>
    /// Registers a handler and the adapter that lets the dispatcher find it.
    /// Both go in together, so a handler can never be registered in a way that
    /// silently never runs.
    /// </summary>
    public static IServiceCollection AddDomainEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IDomainEvent
        where THandler : class, IDomainEventHandler<TEvent>
    {
        services.AddScoped<IDomainEventHandler<TEvent>, THandler>();
        services.AddScoped<IDomainEventHandlerAdapter, DomainEventHandlerAdapter<TEvent>>();

        return services;
    }

    /// <summary>
    /// Turns a configured path into an absolute one. Relative paths are resolved
    /// against the content root so behaviour does not depend on the process's
    /// current directory, which differs between `dotnet run` and a test host.
    /// </summary>
    private static string Resolve(string configuredPath, string contentRootPath) =>
        Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(contentRootPath, configuredPath);
}
