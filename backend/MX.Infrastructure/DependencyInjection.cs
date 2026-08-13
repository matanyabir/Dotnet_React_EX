using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MX.Application.Abstractions;
using MX.Application.Notifications;
using MX.Domain.Common;
using MX.Domain.Tickets.Events;
using MX.Infrastructure.Ai;
using MX.Infrastructure.Configuration;
using MX.Infrastructure.Email;
using MX.Infrastructure.Events;
using MX.Infrastructure.Persistence;
using MX.Infrastructure.Security;
using MX.Infrastructure.Storage;

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

        services.AddSingleton<IFileStorage>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<StorageOptions>>().Value;

            return new LocalFileStorage(
                Resolve(options.UploadsDirectory, contentRootPath),
                options.MaxImageSizeBytes,
                provider.GetRequiredService<ILogger<LocalFileStorage>>());
        });

        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        services.AddSummaryGeneration(configuration);
        services.AddAuthenticationServices(configuration);
        services.AddEmailNotifications(configuration);

        return services;
    }

    /// <summary>
    /// The configured summary provider, wrapped in the resilience decorator.
    /// </summary>
    private static void AddSummaryGeneration(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(AiOptions.SectionName);
        services.Configure<AiOptions>(section);

        var options = section.Get<AiOptions>() ?? new AiOptions();

        // Registered by concrete type; only the decorator is exposed as the port,
        // so nothing can accidentally resolve an unprotected generator.
        switch (options.Provider)
        {
            case AiProvider.Claude:
                services.AddSingleton<ClaudeSummaryGenerator>();
                break;
            case AiProvider.None:
                services.AddSingleton<NullSummaryGenerator>();
                break;
            default:
                services.AddSingleton<StubSummaryGenerator>();
                break;
        }

        services.AddSingleton<ISummaryGenerator>(provider =>
        {
            ISummaryGenerator inner = options.Provider switch
            {
                AiProvider.Claude => provider.GetRequiredService<ClaudeSummaryGenerator>(),
                AiProvider.None => provider.GetRequiredService<NullSummaryGenerator>(),
                _ => provider.GetRequiredService<StubSummaryGenerator>()
            };

            return new ResilientSummaryGenerator(
                inner,
                TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds)),
                provider.GetRequiredService<ILogger<ResilientSummaryGenerator>>());
        });
    }

    /// <summary>
    /// The email sender and the three handlers that use it.
    /// </summary>
    private static void AddEmailNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(EmailOptions.SectionName);
        services.Configure<EmailOptions>(section);

        var options = section.Get<EmailOptions>() ?? new EmailOptions();

        services.AddSingleton(new NotificationSettings(options.FrontendBaseUrl));
        services.AddSingleton<TicketEmailComposer>();

        if (options.Provider is EmailProvider.Smtp)
        {
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            // Registered twice on purpose: once as the port everything consumes,
            // once as itself so tests can read back what was "sent". Resolving the
            // concrete type through the interface registration keeps it one object.
            services.AddSingleton<MockEmailSender>();
            services.AddSingleton<IEmailSender>(provider => provider.GetRequiredService<MockEmailSender>());
        }

        // The README's three cases, one handler each.
        services.AddDomainEventHandler<TicketCreated, SendConfirmationOnTicketCreated>();
        services.AddDomainEventHandler<TicketStatusChanged, SendNoticeOnStatusChanged>();
        services.AddDomainEventHandler<TicketResolutionChanged, SendNoticeOnResolutionChanged>();
    }

    /// <summary>
    /// Password hashing, token issuing, and the account store.
    /// </summary>
    private static void AddAuthenticationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // ValidateOnStart turns a missing or too-short signing key into a startup
        // failure with a clear message, rather than a confusing 500 on the first
        // login attempt hours later.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));

        services.AddSingleton<IPasswordHasher>(_ => new Pbkdf2PasswordHasher());
        services.AddSingleton<IUserStore, ConfiguredUserStore>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
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
