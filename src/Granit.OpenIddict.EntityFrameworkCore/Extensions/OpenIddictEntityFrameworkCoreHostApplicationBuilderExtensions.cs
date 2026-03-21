using System.Diagnostics.CodeAnalysis;
using Granit.OpenIddict.Entities;
using Granit.OpenIddict.EntityFrameworkCore.Internal;
using Granit.OpenIddict.Options;
using Granit.Persistence.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

#pragma warning disable GRSEC003 // OpenIddict permission/grant type constants, not secrets

namespace Granit.OpenIddict.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering OpenIddict EF Core persistence in the host application.
/// </summary>
[ExcludeFromCodeCoverage]
public static class OpenIddictEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the complete Granit OpenIddict stack: ASP.NET Core Identity, OpenIddict
    /// (core + server + validation), and EF Core persistence.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core provider configuration (e.g. <c>options.UseNpgsql(cs)</c>).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitOpenIddictEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        // 1. Register the isolated DbContext with Granit interceptors
        builder.Services.AddGranitDbContext<OpenIddictDbContext>(configure);

        // 2. Register ASP.NET Core Identity
        builder.Services
            .AddIdentity<GranitUser, GranitRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddEntityFrameworkStores<OpenIddictDbContext>()
            .AddDefaultTokenProviders();

        // 3. Register OpenIddict
        OpenIddictBuilder openIddict = builder.Services.AddOpenIddict();

        // 3a. Core — EF Core stores
        openIddict.AddCore(options =>
        {
            options.UseEntityFrameworkCore()
                .UseDbContext<OpenIddictDbContext>()
                .ReplaceDefaultEntities<Guid>();

            // CRITICAL: Disable entity caching by default to prevent cross-tenant pollution.
            // The cache uses ClientId as sole key — two tenants with the same ClientId would
            // share cached data. Enable only for single-tenant deployments.
            options.DisableEntityCaching();
        });

        // 3b. Server — OIDC authorization server
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
                .SetEndUserVerificationEndpointUris("/connect/verify");

            // ──── Flows ────
            options
                .AllowAuthorizationCodeFlow()
                .AllowClientCredentialsFlow()
                .AllowRefreshTokenFlow()
                .AllowDeviceAuthorizationFlow();

            // PKCE required by default (can be relaxed per-application)
            options.RequireProofKeyForCodeExchange();

            // ──── Signing & encryption ────
            // Development keys — host application MUST replace with production keys
            // via options.AddSigningCertificate() / options.AddEncryptionCertificate()
            options
                .AddEphemeralEncryptionKey()
                .AddEphemeralSigningKey();

            // ──── Token formats ────
            // Default: self-contained JWT. Reference tokens enabled via GranitOpenIddictOptions.
            options.DisableAccessTokenEncryption();

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

        // 3c. Validation — token validation for resource servers
        openIddict.AddValidation(options =>
        {
            options.UseLocalServer();
            options.UseAspNetCore();
        });

        return builder;
    }
}

#pragma warning restore GRSEC003
