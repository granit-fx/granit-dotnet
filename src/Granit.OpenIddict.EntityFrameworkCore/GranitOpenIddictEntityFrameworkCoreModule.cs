using Granit.Encryption;
using Granit.Http.Cookies;
using Granit.Identity.Local;
using Granit.Identity.Local.Options;
using Granit.Identity.Local.Services;
using Granit.Modularity;
using Granit.MultiTenancy;
using Granit.OpenIddict.EntityFrameworkCore.Internal;
using Granit.OpenIddict.EntityFrameworkCore.Seeding;
using Granit.OpenIddict.Options;
using Granit.OpenIddict.Server;
using Granit.OpenIddict.Services;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.DataSeeding;
using Granit.Persistence.EntityFrameworkCore.ExtraProperties;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenIddict.Server;

namespace Granit.OpenIddict.EntityFrameworkCore;

/// <summary>
/// Granit module that registers EF Core persistence for OpenIddict.
/// </summary>
/// <remarks>
/// <para>
/// The host application must configure the <see cref="Internal.OpenIddictDbContext"/>
/// connection string via <c>AddGranitOpenIddict(configure)</c>.
/// This module registers the store implementations, OpenIddict core services,
/// the declarative seed contributor, and passkey options.
/// </para>
/// <para>
/// Depends on <see cref="GranitMultiTenancyModule"/> for GDPR-strict tenant isolation
/// in OpenIddict stores, and <see cref="GranitEncryptionModule"/> for signing key
/// material encryption at rest.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitEncryptionModule),
    typeof(GranitIdentityLocalModule),
    typeof(GranitMultiTenancyModule),
    typeof(GranitOpenIddictServerModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitOpenIddictEntityFrameworkCoreModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services
            .AddOptions<GranitOpenIddictSeedingOptions>()
            .BindConfiguration(GranitOpenIddictSeedingOptions.SectionName);

        context.Services
            .AddOptions<GranitPasskeyOptions>()
            .BindConfiguration(GranitPasskeyOptions.SectionName);

        context.Services.AddTransient<IDataSeedContributor, OpenIddictSeedContributor>();
        context.Services.AddSingleton<ICookieDefinitionContributor, IdentityCookieDefinitionContributor>();

        // Override default ASP.NET Core Identity cookie names to avoid leaking the technology stack.
        // Uses __Host- prefix for CSRF-hardening (Secure + Path=/ + no Domain).
        // Only targets Identity schemes — does NOT use ConfigureAll to avoid breaking BFF/OIDC cookies.
        ConfigureIdentityCookie(context.Services,
            Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme,
            IdentityCookieDefinitionContributor.DefaultApplicationCookieName);

        ConfigureIdentityCookie(context.Services,
            Microsoft.AspNetCore.Identity.IdentityConstants.TwoFactorUserIdScheme,
            IdentityCookieDefinitionContributor.DefaultTwoFactorCookieName);

        ConfigureIdentityCookie(context.Services,
            Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme,
            IdentityCookieDefinitionContributor.DefaultExternalCookieName);

        context.Services.TryAddScoped<ILocalIdentityGroupStore, OpenIddictGroupStore>();
        context.Services.TryAddScoped<ExternalClaimsMapper>();
        context.Services.TryAddScoped<IExternalLoginService, AspNetExternalLoginService>();
        context.Services.TryAddScoped<ITotpService, TotpService>();
        context.Services.TryAddScoped<ITwoFactorService, AspNetTwoFactorService>();
        context.Services.TryAddScoped<IAccountDeletionService, AspNetAccountDeletionService>();
        context.Services.TryAddScoped<IPasswordResetService, AspNetPasswordResetService>();
        context.Services.TryAddScoped<ISigningKeyStore, EfSigningKeyStore>();
        context.Services.TryAddScoped<IKeyRotationService, KeyRotationService>();
        context.Services.TryAddScoped<IPasskeyService, AspNetPasskeyService>();
        context.Services.TryAddScoped<IImpersonationService, AspNetImpersonationService>();

        context.Services
            .AddOptions<GranitKeyRotationOptions>()
            .BindConfiguration(GranitKeyRotationOptions.SectionName);

        // GranitUser implements IHasExtraProperties — apps can extend user properties
        // by calling AddExtraPropertyMappings<GranitUser> in their own module.
        // The ExtraPropertySyncInterceptor in Granit.Persistence handles sync automatically.
        context.Services.AddExtraPropertyInfrastructure();

        // Load signing/encryption keys from DB at startup (replaces ephemeral keys)
        context.Services.AddSingleton<IPostConfigureOptions<OpenIddictServerOptions>,
            DatabaseSigningKeyPostConfigure>();
    }

    private static void ConfigureIdentityCookie(
        IServiceCollection services, string scheme, string cookieName)
    {
        services.Configure<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>(
            scheme,
            options =>
            {
                options.Cookie.Name = cookieName;
                options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
            });
    }
}
