using Granit.Diagnostics;
using Granit.Http.Cookies;
using Granit.Identity.Local;
using Granit.Identity.Local.Options;
using Granit.Identity.Local.Services;
using Granit.Modularity;
using Granit.OpenIddict.Diagnostics;
using Granit.OpenIddict.Internal;
using Granit.OpenIddict.Options;
using Granit.OpenIddict.Services;
using Granit.QueryEngine;
using Granit.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.OpenIddict;

/// <summary>
/// Granit module for the OpenIddict-based identity provider.
/// Registers OIDC abstractions, service interfaces, integration event types,
/// and Identity cookie configuration (neutral names, env-aware __Host- prefix).
/// </summary>
[DependsOn(
    typeof(GranitHttpCookiesModule),
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
            .BindConfiguration("OpenIddict");

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

        // Identity cookie configuration — neutral names to avoid leaking the technology stack.
        // PostConfigure is required because AddIdentity<TUser, TRole>() registers its own
        // PostConfigure that resets names to ASP.NET Core defaults.
        context.Services.AddSingleton<ICookieDefinitionContributor, IdentityCookieDefinitionContributor>();

        bool isDevelopment = context.Builder!.Environment.IsDevelopment();

        PostConfigureIdentityCookie(context.Services, isDevelopment,
            Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme,
            IdentityCookieDefinitionContributor.DefaultApplicationCookieName,
            IdentityCookieDefinitionContributor.DevApplicationCookieName);

        PostConfigureIdentityCookie(context.Services, isDevelopment,
            Microsoft.AspNetCore.Identity.IdentityConstants.TwoFactorUserIdScheme,
            IdentityCookieDefinitionContributor.DefaultTwoFactorCookieName,
            IdentityCookieDefinitionContributor.DevTwoFactorCookieName);

        PostConfigureIdentityCookie(context.Services, isDevelopment,
            Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme,
            IdentityCookieDefinitionContributor.DefaultExternalCookieName,
            IdentityCookieDefinitionContributor.DevExternalCookieName);
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
