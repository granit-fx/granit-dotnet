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

        // PostConfigure s'exécute après AddGranitJwtBearer (GranitJwtBearerModule),
        // permettant de surcharger Authority, Audience et NameClaimType pour Keycloak.
        // Deferred configuration: reads KeycloakOptions at resolution time.
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .PostConfigure<IOptions<KeycloakOptions>>((jwt, keycloakOpts) =>
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
