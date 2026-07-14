using System.Diagnostics.CodeAnalysis;
using Granit.Authentication.OpenIddict.Options;
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

                // OpenIddict's built-in extractor only accepts the Bearer scheme. Register the
                // DPoP-scheme token extractor (RFC 9449 §7.1) so a remote resource server can
                // authenticate `Authorization: DPoP <token>` requests. Harmless when no DPoP
                // client is used — it only acts as a fallback after the Bearer extractor.
                options.AddEventHandler(Handlers.DPoPValidationTokenExtractionHandler.Descriptor);
            });

        // Normalize OIDC short-name "role" claims emitted by OpenIddict.Validation into
        // ClaimTypes.Role so PermissionChecker.AdminRoles bypass and ICurrentUserService
        // .GetRoles() agree with ClaimsPrincipal.IsInRole(). Mirrors what JwtBearer does
        // implicitly via TokenValidationParameters.RoleClaimType.
        builder.Services.AddGranitOpenIddictRoleClaimNormalization();

        return builder;
    }
}
