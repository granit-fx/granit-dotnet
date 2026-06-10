using Granit.Caching;
using Granit.DataExchange;
using Granit.DataExchange.Extensions;
using Granit.Diagnostics;
using Granit.Encryption;
using Granit.Entities;
using Granit.Entities.Extensions;
using Granit.Http.Cookies;
using Granit.Identity;
using Granit.Identity.Local;
using Granit.Identity.Local.AspNetIdentity;
using Granit.Identity.Local.Options;
using Granit.Identity.Local.Services;
using Granit.Modularity;
using Granit.OpenIddict.Diagnostics;
using Granit.OpenIddict.Entities;
using Granit.OpenIddict.Entities.OpenIddict;
using Granit.OpenIddict.Exports;
using Granit.OpenIddict.Internal;
using Granit.OpenIddict.Options;
using Granit.OpenIddict.Queries;
using Granit.OpenIddict.Services;
using Granit.QueryEngine;
using Granit.QueryEngine.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenIddict.Server;

namespace Granit.OpenIddict;

/// <summary>
/// Granit module for the OpenIddict-based identity provider.
/// Registers OIDC abstractions, service interfaces, integration event types,
/// and Identity cookie configuration (neutral names, env-aware __Host- prefix).
/// </summary>
[DependsOn(
    typeof(GranitCachingModule),
    typeof(GranitDataExchangeAbstractionsModule),
    typeof(GranitEncryptionModule),
    typeof(GranitEntitiesAbstractionsModule),
    typeof(GranitHttpCookiesModule),
    typeof(GranitIdentityLocalAspNetIdentityModule),
    typeof(GranitIdentityLocalModule),
    typeof(GranitQueryEngineAbstractionsModule))]
public sealed class GranitOpenIddictModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddSingleton<OpenIddictMetrics>();
        GranitActivitySourceRegistry.Register(OpenIddictActivitySource.Name);

        context.Services
            .AddOptions<GranitOpenIddictOptions>()
            .BindConfiguration(GranitOpenIddictOptions.SectionName);

        context.Services
            .AddOptions<GranitOpenIddictClientOptions>()
            .BindConfiguration(GranitOpenIddictClientOptions.SectionName);

        context.Services
            .AddOptions<GranitPasskeyOptions>()
            .BindConfiguration(GranitPasskeyOptions.SectionName);

        context.Services
            .AddOptions<GranitOpenIddictSeedingOptions>()
            .BindConfiguration(GranitOpenIddictSeedingOptions.SectionName);

        context.Services
            .AddOptions<GranitKeyRotationOptions>()
            .BindConfiguration(GranitKeyRotationOptions.SectionName);

        context.Services.TryAddScoped<IClaimsDestinationProvider, DefaultClaimsDestinationProvider>();
        context.Services.TryAddScoped<ITotpService, DefaultTotpService>();
        context.Services.TryAddScoped<ExternalClaimsMapper>();
        context.Services.TryAddScoped<IExternalLoginService, Internal.AspNetExternalLoginService>();
        context.Services.TryAddScoped<IAccountDeletionService, Internal.AspNetAccountDeletionService>();
        context.Services.TryAddScoped<IImpersonationService, Internal.AspNetImpersonationService>();
        context.Services.TryAddScoped<IKeyRotationService, Internal.KeyRotationService>();
        context.Services.TryAddSingleton<IExternalProviderRegistry, OpenIddictExternalProviderRegistry>();

        // Load signing/encryption keys from DB at startup (replaces ephemeral keys)
        context.Services.AddSingleton<IPostConfigureOptions<OpenIddictServerOptions>,
            DatabaseSigningKeyPostConfigure>();

        // Identity cookie configuration — neutral names to avoid leaking the technology stack.
        // PostConfigure is required because AddIdentity<TUser, TRole>() registers its own
        // PostConfigure that resets names to ASP.NET Core defaults.
        context.Services.AddSingleton<ICookieDefinitionContributor, IdentityCookieDefinitionContributor>();

        bool isDevelopment = context.Builder!.Environment.IsDevelopment();

        PostConfigureIdentityCookie(context.Services, isDevelopment,
            Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme,
            IdentityCookieDefinitionContributor.DefaultApplicationCookieName,
            IdentityCookieDefinitionContributor.DevApplicationCookieName);

        // When an incoming request carries an `Authorization` header, forward the
        // Identity.Application cookie scheme to OpenIddict token validation (Bearer).
        // BFF-integrated deployments serve the same backend to multiple SPAs (e.g.
        // /host and /app) and the Identity cookie is shared across them (localhost
        // in dev, shared parent domain in production). A user logging in on one
        // side overwrites the cookie, and without this forward the cookie-authenticated
        // principal wins over the BFF-injected Bearer token — authorising the request
        // as the wrong user (403 on host admin endpoints once a tenant user has logged
        // in, and vice-versa). Requests without an `Authorization` header (e.g. the
        // OIDC server's `/connect/*` endpoints) keep using cookie auth unchanged.
        context.Services.PostConfigure<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>(
            Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme,
            options =>
            {
                // Scheme name is the well-known OpenIddict validation scheme. Using the
                // string literal (rather than `OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme`)
                // avoids forcing every Granit.OpenIddict consumer to pull in
                // `OpenIddict.Validation.AspNetCore` as a direct reference — this base
                // module stays free of the ASP.NET Core validation integration and the
                // constant stays pinned to the OpenIddict contract.
                options.ForwardDefaultSelector = httpContext =>
                    httpContext.Request.Headers.ContainsKey(Microsoft.Net.Http.Headers.HeaderNames.Authorization)
                        ? "OpenIddict.Validation.AspNetCore"
                        : null;
            });

        PostConfigureIdentityCookie(context.Services, isDevelopment,
            Microsoft.AspNetCore.Identity.IdentityConstants.TwoFactorUserIdScheme,
            IdentityCookieDefinitionContributor.DefaultTwoFactorCookieName,
            IdentityCookieDefinitionContributor.DevTwoFactorCookieName);

        PostConfigureIdentityCookie(context.Services, isDevelopment,
            Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme,
            IdentityCookieDefinitionContributor.DefaultExternalCookieName,
            IdentityCookieDefinitionContributor.DevExternalCookieName);

        // Query + Export definitions (ADR-020: owned by the base module).
        context.Services.AddQueryDefinition<GranitOpenIddictApplication, GranitOpenIddictApplicationQueryDefinition>();
        context.Services.AddQueryDefinition<GranitOpenIddictScope, GranitOpenIddictScopeQueryDefinition>();
        context.Services.AddExportDefinition<GranitOpenIddictApplication, OpenIddictApplicationExportDefinition>();
        context.Services.AddExportDefinition<GranitOpenIddictScope, OpenIddictScopeExportDefinition>();

        // Phase 2 EntityDefinitions (ADR-050).
        context.Services.AddEntityDefinition<GranitOpenIddictApplication, GranitOpenIddictApplicationEntityDefinition>();
        context.Services.AddEntityDefinition<GranitOpenIddictScope, GranitOpenIddictScopeEntityDefinition>();

        // Override the stub IIdentitySessionManager from AspNetIdentityProvider with an
        // implementation that reads active refresh tokens from the OpenIddict token store.
        context.Services.TryAddScoped<OpenIddictSessionManager>();
        context.Services.Replace(ServiceDescriptor.Scoped<IIdentitySessionManager>(
            sp => sp.GetRequiredService<OpenIddictSessionManager>()));
    }

    /// <inheritdoc/>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        // Fail fast on a config/scheme mismatch: every external provider listed under
        // OpenIddict:Client:Providers is advertised to users as available, so each MUST have a
        // registered authentication handler. A provider without its scheme (host forgot
        // AddGoogle()/AddMicrosoftAccount()) would otherwise pass IsProviderConfigured and only
        // fail when a user clicks "Sign in with X" and the redirect dead-ends.
        GranitOpenIddictClientOptions clientOptions = context.ServiceProvider
            .GetRequiredService<IOptions<GranitOpenIddictClientOptions>>().Value;

        if (clientOptions.Providers.Length == 0)
        {
            return;
        }

        IAuthenticationSchemeProvider schemeProvider = context.ServiceProvider
            .GetRequiredService<IAuthenticationSchemeProvider>();

        var registeredSchemes = schemeProvider.GetAllSchemesAsync()
            .GetAwaiter().GetResult()
            .Select(s => s.Name)
            .ToHashSet(StringComparer.Ordinal);

        string[] missing = clientOptions.Providers
            .Select(p => p.Name)
            .Where(name => !registeredSchemes.Contains(name))
            .ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"External login provider(s) [{string.Join(", ", missing)}] are configured under "
                + $"'{GranitOpenIddictClientOptions.SectionName}:Providers' but have no registered "
                + "authentication handler. Register each provider's scheme on the host "
                + "(e.g. services.AddAuthentication().AddGoogle(...)) with a scheme name matching the "
                + "configured provider Name, or remove the provider from configuration.");
        }
    }

    private static void PostConfigureIdentityCookie(
        IServiceCollection services, bool isDevelopment, string scheme,
        string prodCookieName, string devCookieName)
    {
        services.PostConfigure<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>(
            scheme,
            options =>
            {
                options.Cookie.Name = isDevelopment ? devCookieName : prodCookieName;
                options.Cookie.SecurePolicy = isDevelopment
                    ? Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest
                    : Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
            });
    }
}
