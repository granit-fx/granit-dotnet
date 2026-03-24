using Granit.Authentication.JwtBearer.EntraId.Authentication;
using Granit.Authentication.JwtBearer.EntraId.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Authentication.JwtBearer.EntraId.Extensions;

/// <summary>
/// Extensions to configure Entra ID extras on <c>Granit.Authentication.JwtBearer</c>.
/// </summary>
public static class EntraIdServiceCollectionExtensions
{
    /// <summary>
    /// Overrides JWT Bearer configuration with Entra ID values,
    /// registers <see cref="EntraIdClaimsTransformation"/> and the <c>"Admin"</c> policy.
    /// </summary>
    public static IServiceCollection AddGranitEntraId(
        this IServiceCollection services)
    {
        services
            .AddOptions<EntraIdOptions>()
            .BindConfiguration(EntraIdOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // PostConfigure runs after AddGranitJwtBearer (GranitJwtBearerModule),
        // allowing us to override Authority, Audience and NameClaimType for Entra ID.
        // Deferred configuration: reads EntraIdOptions at resolution time.
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .PostConfigure<IOptions<EntraIdOptions>>((jwt, entraIdOpts) =>
            {
                EntraIdOptions options = entraIdOpts.Value;
                jwt.Authority = options.Authority;
                jwt.Audience = options.ClientId;
                jwt.RequireHttpsMetadata = options.RequireHttpsMetadata;
                jwt.TokenValidationParameters.NameClaimType = "preferred_username";
                jwt.TokenValidationParameters.ValidIssuer = options.Authority;
                jwt.TokenValidationParameters.ValidAudience = options.ClientId;
            });

        services.AddTransient<IClaimsTransformation, EntraIdClaimsTransformation>();

        // Deferred Admin policy: reads AdminRole from EntraIdOptions at resolution time.
        services
            .AddOptions<AuthorizationOptions>()
            .Configure<IOptions<EntraIdOptions>>((authOpts, entraIdOpts) =>
            {
                authOpts.AddPolicy("Admin",
                    policy => policy.RequireRole(entraIdOpts.Value.AdminRole));
            });

        return services;
    }
}
