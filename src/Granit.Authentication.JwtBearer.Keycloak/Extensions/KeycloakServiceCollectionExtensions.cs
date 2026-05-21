using Granit.Authentication.JwtBearer.Keycloak.Authentication;
using Granit.Authentication.JwtBearer.Keycloak.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Authentication.JwtBearer.Keycloak.Extensions;

/// <summary>
/// Extensions pour configurer les extras Keycloak sur <c>Granit.Authentication.JwtBearer</c>.
/// </summary>
public static class KeycloakServiceCollectionExtensions
{
    /// <summary>
    /// Surcharge la configuration JWT Bearer avec les valeurs Keycloak
    /// et enregistre <see cref="KeycloakClaimsTransformation"/>.
    /// </summary>
    public static IServiceCollection AddGranitKeycloak(
        this IServiceCollection services)
    {
        services
            .AddOptions<KeycloakOptions>()
            .BindConfiguration(KeycloakOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Configure (not PostConfigure) so Keycloak's Authority / Audience are
        // applied BEFORE the framework's JwtBearerPostConfigureOptions runs:
        // that built-in PostConfigure is the one that materialises
        // ConfigurationManager<OpenIdConnectConfiguration> from Authority. If
        // Authority is still empty at that point, no ConfigurationManager is
        // built, JWKS is never fetched, and every inbound token fails with
        // IDX10500 ("Signature validation failed. Unable to resolve
        // SignatureValidator or SecurityTokenSignatureValidator"). PostConfigure
        // here would run too late.
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<KeycloakOptions>>((jwt, keycloakOpts) =>
            {
                KeycloakOptions options = keycloakOpts.Value;
                string audience = options.Audience ?? options.ClientId;
                jwt.Authority = options.Authority;
                jwt.Audience = audience;
                jwt.RequireHttpsMetadata = options.RequireHttpsMetadata;
                jwt.TokenValidationParameters.NameClaimType = "preferred_username";
                jwt.TokenValidationParameters.ValidIssuer = options.Authority;
                jwt.TokenValidationParameters.ValidAudience = audience;
            });

        services.AddTransient<IClaimsTransformation, KeycloakClaimsTransformation>();

        return services;
    }
}
