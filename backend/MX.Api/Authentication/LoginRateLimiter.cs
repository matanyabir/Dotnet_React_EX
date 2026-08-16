using System.Globalization;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using MX.Infrastructure.Configuration;

namespace MX.Api.Authentication;

/// <summary>
/// A cap on how often one client may attempt to sign in.
///
/// Every other route either changes nothing or already demands a valid token.
/// Sign-in is the exception on both counts: it is anonymous by necessity, and
/// guessing at it repeatedly is a strategy that works given enough tries. A
/// short, uniform password is minutes of unattended requests away from being
/// found, and each guess also costs this API a deliberately expensive PBKDF2
/// verification — so an unlimited door is both a way in and a way to exhaust
/// the CPU of everyone else's requests.
///
/// Two decisions are worth stating, because the obvious alternatives are worse:
///
/// 1. <b>Counted per client address, not per account.</b> Limiting by the email
///    in the request would let anyone lock a named admin out of their own
///    account by failing five logins on their behalf — turning a protection
///    into a denial-of-service tool aimed at exactly the people who need in.
/// 2. <b>Every attempt counts, not only the failures.</b> Refunding a correct
///    password would let an attacker who holds one valid credential keep an
///    unlimited budget for guessing at the others.
///
/// <para>
/// Behind a reverse proxy this counts the proxy, not the caller, and collapses
/// every client into one bucket. Deploying that way needs
/// <c>UseForwardedHeaders</c> with the proxy named in <c>KnownProxies</c> — it
/// is left out here on purpose, because enabling it without pinning the trusted
/// proxy lets any caller spoof <c>X-Forwarded-For</c> and mint a fresh bucket
/// per request, which is worse than not having the limit at all.
/// </para>
/// </summary>
internal static class LoginRateLimiter
{
    /// <summary>Named rather than global: only sign-in carries this policy.</summary>
    public const string PolicyName = "login";

    public static IServiceCollection AddLoginRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(LoginRateLimitOptions.SectionName);

        // Validated at startup for the same reason the signing key is: a nonsense
        // window should fail the boot with a clear message, not quietly permit
        // everything until someone notices.
        services.AddOptions<LoginRateLimitOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var limits = section.Get<LoginRateLimitOptions>() ?? new LoginRateLimitOptions();

        services.AddRateLimiter(options =>
        {
            // 429 rather than the 503 default: the caller is being asked to slow
            // down, not told the service is broken.
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(PolicyName, context =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.PermitLimit,
                        Window = limits.Window,

                        // Queue nothing. Holding a login request until a permit
                        // frees up would leave the browser waiting on a spinner
                        // for minutes; being told "not now" is the useful answer.
                        QueueLimit = 0
                    }));

            options.OnRejected = (context, _) => RejectAsync(context, limits.Window);
        });

        return services;
    }

    /// <summary>
    /// Answers a throttled attempt in the same ProblemDetails shape every other
    /// failure here uses, so the frontend renders it through the path it already
    /// has rather than needing a special case for this one status.
    ///
    /// The message says nothing about the account that was tried — the limit is
    /// per address and behaves identically for a real email and an invented one,
    /// which keeps the "you cannot tell which accounts exist" property that
    /// <see cref="MX.Application.Auth.AuthService"/> goes to some trouble for.
    /// </summary>
    private static async ValueTask RejectAsync(OnRejectedContext context, TimeSpan window)
    {
        var httpContext = context.HttpContext;

        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var metadata)
            ? metadata
            : window;

        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));

        // The machine-readable half of the same statement the body makes.
        httpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);

        // A run of these is what a brute-force attempt looks like from the
        // inside, and it is worth nothing if no one can see it after the fact.
        httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(LoginRateLimiter))
            .LogWarning(
                "Sign-in rate limit reached for {Client}; rejecting for {RetryAfterSeconds}s.",
                ClientKey(httpContext),
                seconds);

        await Results.Problem(
                title: "Too many sign-in attempts",
                detail: $"Too many sign-in attempts. Try again in {Describe(seconds)}.",
                statusCode: StatusCodes.Status429TooManyRequests,

                // Stated explicitly because ASP.NET has no default for 429 — the
                // status predates RFC 9110 and is defined in RFC 6585 instead.
                // Left out, this would be the one error here missing a "type".
                type: "https://tools.ietf.org/html/rfc6585#section-4")
            .ExecuteAsync(httpContext);
    }

    /// <summary>
    /// The bucket an attempt counts against.
    ///
    /// IPv6 is grouped by its /64 prefix because a single subscriber is routinely
    /// handed an entire /64. Counting per address there would let one client step
    /// to a fresh address for every guess and never meet the limit.
    /// </summary>
    private static string ClientKey(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;

        if (address is null)
        {
            // No connection information — an in-memory test host, or an unusual
            // transport. One shared bucket limits rather than exempts, which is
            // the safe reading of "we do not know who this is".
            return "unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily is not AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        return Convert.ToHexString(address.GetAddressBytes().AsSpan(0, 8)) + "::/64";
    }

    /// <summary>A wait a person can act on, rather than a raw second count.</summary>
    private static string Describe(int seconds)
    {
        if (seconds < 60)
        {
            return seconds == 1 ? "a second" : $"{seconds} seconds";
        }

        var minutes = (seconds + 59) / 60;

        return minutes == 1 ? "a minute" : $"{minutes} minutes";
    }
}
