using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MX.Domain.Users;
using MX.Infrastructure.Configuration;

namespace MX.Api.Authentication;

/// <summary>
/// Bearer-token validation and the authorization policies that use it.
/// </summary>
internal static class AuthenticationSetup
{
    /// <summary>Only holders of this policy may edit tickets.</summary>
    public const string AdminPolicy = "Admin";

    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // Every one of these is on deliberately. Turning any of them
                    // off would accept tokens this API never issued: an unsigned
                    // token, one minted for a different service, or an expired one.
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,

                    // The default is five minutes of slack, which keeps expired
                    // tokens working well past their stated expiry.
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AdminPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(UserRoles.Admin));

        return services;
    }
}
