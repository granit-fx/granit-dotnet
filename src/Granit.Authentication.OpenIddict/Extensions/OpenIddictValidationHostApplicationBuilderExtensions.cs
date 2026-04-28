using System.Diagnostics.CodeAnalysis;
using Granit.Authentication.OpenIddict.Internal;
using Granit.Authentication.OpenIddict.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Granit.Authentication.OpenIddict.Extensions;

/// <summary>
/// Extension methods for registering OpenIddict token validation on resource servers.
/// </summary>
[ExcludeFromCodeCoverage]
public static class OpenIddictValidationHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers OpenIddict remote token validation. Validates JWT/reference tokens
    /// from a remote Granit OpenIddict server via the discovery endpoint.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitOpenIddictAuthentication(
        this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        GranitOpenIddictValidationOptions validationOptions = new();
        builder.Configuration
            .GetSection(GranitOpenIddictValidationOptions.SectionName)
            .Bind(validationOptions);

        builder.Services.AddOpenIddict()
            .AddValidation(options =>
            {
                if (validationOptions.Issuer is not null)
                {
                    options.SetIssuer(validationOptions.Issuer);
                }

                if (!string.IsNullOrEmpty(validationOptions.Audience))
                {
                    options.AddAudiences(validationOptions.Audience);
                }

                options.UseSystemNetHttp();
                options.UseAspNetCore();
            });

        // Normalize OIDC short-name "role" claims emitted by OpenIddict.Validation into
        // ClaimTypes.Role so PermissionChecker.AdminRoles bypass and ICurrentUserService
        // .GetRoles() agree with ClaimsPrincipal.IsInRole(). Mirrors what JwtBearer does
        // implicitly via TokenValidationParameters.RoleClaimType.
        builder.Services.AddGranitOpenIddictRoleClaimNormalization();

        // Store RequireDPoP flag for middleware registration
        if (validationOptions.RequireDPoP)
        {
            builder.Services.AddSingleton(validationOptions);
        }

        return builder;
    }

    /// <summary>
    /// Adds DPoP enforcement middleware when <see cref="GranitOpenIddictValidationOptions.RequireDPoP"/>
    /// is enabled. Must be called <strong>after</strong> <c>UseAuthentication()</c>.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseGranitDPoPEnforcement(this IApplicationBuilder app)
    {
        GranitOpenIddictValidationOptions? options = app.ApplicationServices
            .GetService<GranitOpenIddictValidationOptions>();

        if (options?.RequireDPoP == true)
        {
            app.UseMiddleware<RequireDPoPMiddleware>();
        }

        return app;
    }
}
