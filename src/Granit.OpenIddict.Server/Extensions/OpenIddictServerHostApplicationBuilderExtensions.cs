using System.Diagnostics.CodeAnalysis;
using Granit.OpenIddict.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;

#pragma warning disable GRSEC003 // OpenIddict permission/grant type constants, not secrets

namespace Granit.OpenIddict.Server.Extensions;

/// <summary>
/// Extension methods for registering the OpenIddict OIDC server and local validation.
/// </summary>
[ExcludeFromCodeCoverage]
public static class OpenIddictServerHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the OpenIddict server (authorization endpoints, flows, signing keys,
    /// custom grant types) and local token validation.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitOpenIddictServer(
        this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Read options from configuration for build-time decisions
        GranitOpenIddictOptions granitOptions = new();
        builder.Configuration.GetSection("OpenIddict").Bind(granitOptions);

        OpenIddictBuilder openIddict = builder.Services.AddOpenIddict();

        // ──── Server — OIDC authorization server ────
        openIddict.AddServer(options =>
        {
            // ──── Endpoints ────
            options
                .SetAuthorizationEndpointUris("/connect/authorize")
                .SetTokenEndpointUris("/connect/token")
                .SetUserInfoEndpointUris("/connect/userinfo")
                .SetIntrospectionEndpointUris("/connect/introspect")
                .SetRevocationEndpointUris("/connect/revoke")
                .SetEndSessionEndpointUris("/connect/logout")
                .SetDeviceAuthorizationEndpointUris("/connect/device")
                .SetEndUserVerificationEndpointUris("/connect/verify")
                .SetPushedAuthorizationEndpointUris("/connect/par");

            // ──── Flows ────
            options
                .AllowAuthorizationCodeFlow()
                .AllowClientCredentialsFlow()
                .AllowRefreshTokenFlow()
                .AllowDeviceAuthorizationFlow();

            // PKCE required by default (can be relaxed per-application)
            options.RequireProofKeyForCodeExchange();

            // PAR enforcement (optional — endpoint is always available via SetPushedAuthorizationEndpointUris)
            if (granitOptions.RequirePar)
            {
                options.RequirePushedAuthorizationRequests();
            }

            // ──── Signing & encryption ────
            // Development keys — host application MUST replace with production keys
            // via options.AddSigningCertificate() / options.AddEncryptionCertificate()
            options
                .AddEphemeralEncryptionKey()
                .AddEphemeralSigningKey();

            // ──── Issuer ────
            if (granitOptions.Issuer is not null)
            {
                options.SetIssuer(granitOptions.Issuer);
            }

            // ──── Token formats ────
            options.DisableAccessTokenEncryption();

            // Reference tokens: opaque tokens validated against DB on every request.
            // Enables instant revocation at the cost of +1 DB round-trip per API call.
            if (granitOptions.UseReferenceTokens)
            {
                options.UseReferenceAccessTokens();
                options.UseReferenceRefreshTokens();
            }

            // ──── ASP.NET Core integration ────
            options
                .UseAspNetCore()
                .EnableAuthorizationEndpointPassthrough()
                .EnableTokenEndpointPassthrough()
                .EnableUserInfoEndpointPassthrough()
                .EnableEndSessionEndpointPassthrough()
                .EnableEndUserVerificationEndpointPassthrough()
                .EnableStatusCodePagesIntegration();

            // ──── Custom grant types ────
            options.AllowCustomFlow("urn:granit:grant_type:two_factor");
            options.AllowCustomFlow("urn:granit:grant_type:passkey");

            // ──── Scopes ────
            options.RegisterScopes(
                OpenIddictConstants.Scopes.Email,
                OpenIddictConstants.Scopes.Profile,
                OpenIddictConstants.Scopes.Roles,
                "offline_access");
        });

        // ──── Validation — token validation for resource servers ────
        openIddict.AddValidation(options =>
        {
            options.UseLocalServer();
            options.UseAspNetCore();
        });

        return builder;
    }
}

#pragma warning restore GRSEC003
