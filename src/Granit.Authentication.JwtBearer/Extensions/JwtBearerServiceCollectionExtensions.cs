using Granit.Authentication.JwtBearer.Authentication;
using Granit.Authentication.JwtBearer.BackChannelLogout;
using Granit.Authentication.JwtBearer.Options;
using Granit.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Granit.Authentication.JwtBearer.Extensions;

/// <summary>
/// Extensions for configuring generic JWT Bearer authentication and <see cref="ICurrentUserService"/>.
/// </summary>
public static class JwtBearerServiceCollectionExtensions
{
    /// <summary>
    /// Adds generic OIDC JWT Bearer authentication and the CurrentUser service.
    /// Reads the <c>"Authentication"</c> section from configuration.
    /// </summary>
    // Sentinel: prevents double-registration when AddGranitAsync is called multiple times
    // (e.g. AddSharedHostingAsync + a second service-specific AddGranitAsync call).
    private sealed class GranitJwtBearerRegistered;

    public static IServiceCollection AddGranitJwtBearer(
        this IServiceCollection services)
    {
        if (services.Any(sd => sd.ServiceType == typeof(GranitJwtBearerRegistered)))
        {
            return services;
        }

        services.AddSingleton<GranitJwtBearerRegistered>();

        services
            .AddOptions<JwtBearerAuthOptions>()
            .BindConfiguration(JwtBearerAuthOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Deferred configuration: JwtBearerOptions reads JwtBearerAuthOptions at resolution time.
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtBearerAuthOptions>>((jwt, granitOpts) =>
            {
                JwtBearerAuthOptions options = granitOpts.Value;
                jwt.Authority = options.Authority;
                jwt.Audience = options.Audience;
                jwt.RequireHttpsMetadata = options.RequireHttpsMetadata;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = options.Authority,
                    ValidAudience = options.Audience,
                    NameClaimType = options.NameClaimType,
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("Authenticated", policy => policy.RequireAuthenticatedUser());

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Back-channel logout: revoked session store + token validator
        services.AddDistributedMemoryCache();
        services.TryAddSingleton<IRevokedSessionStore, DistributedCacheRevokedSessionStore>();
        services.TryAddSingleton<BackChannelLogoutTokenValidator>();

        // Wire OnTokenValidated to check revoked sessions when back-channel logout is enabled
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .PostConfigure<IOptions<JwtBearerAuthOptions>>((jwt, authOpts) =>
            {
                if (!authOpts.Value.BackChannelLogout.Enabled)
                {
                    return;
                }

                JwtBearerEvents existing = jwt.Events ?? new JwtBearerEvents();
                Func<TokenValidatedContext, Task> previous = existing.OnTokenValidated;

                existing.OnTokenValidated = async context =>
                {
                    await previous(context).ConfigureAwait(false);

                    IRevokedSessionStore store = context.HttpContext.RequestServices
                        .GetRequiredService<IRevokedSessionStore>();

                    string? sid = context.Principal?.FindFirst("sid")?.Value;
                    string? sub = context.Principal?.FindFirst("sub")?.Value;
                    string? sessionKey = sid ?? sub;

                    if (sessionKey is not null
                        && await store.IsSessionRevokedAsync(sessionKey, context.HttpContext.RequestAborted).ConfigureAwait(false))
                    {
                        context.Fail("Session has been revoked via back-channel logout.");
                    }
                };

                jwt.Events = existing;
            });

        return services;
    }
}
