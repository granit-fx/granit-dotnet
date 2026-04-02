using Granit.Encryption;
using Granit.Identity.Local;
using Granit.Identity.Local.Services;
using Granit.Modularity;
using Granit.MultiTenancy;
using Granit.OpenIddict.EntityFrameworkCore.Internal;
using Granit.OpenIddict.EntityFrameworkCore.Seeding;
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
/// Granit module that registers EF Core persistence and ASP.NET Core Identity
/// service implementations for OpenIddict.
/// </summary>
/// <remarks>
/// <para>
/// Persistence: <see cref="Internal.OpenIddictDbContext"/>, EF stores, data seeding.
/// Identity services: <c>AspNet*</c> implementations that depend on <c>UserManager&lt;GranitUser&gt;</c>
/// (registered here because <c>AddIdentity()</c> is called in <c>AddGranitOpenIddict()</c>).
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
        // Data seeding
        context.Services.AddTransient<IDataSeedContributor, OpenIddictSeedContributor>();

        // EF Core stores
        context.Services.TryAddScoped<ILocalIdentityGroupStore, OpenIddictGroupStore>();
        context.Services.TryAddScoped<ISigningKeyStore, EfSigningKeyStore>();
        context.Services.TryAddScoped<IKeyRotationService, KeyRotationService>();

        // ASP.NET Core Identity service implementations (depend on UserManager<GranitUser>)
        context.Services.TryAddScoped<IExternalLoginService, AspNetExternalLoginService>();
        context.Services.TryAddScoped<ITotpService, TotpService>();
        context.Services.TryAddScoped<ITwoFactorService, AspNetTwoFactorService>();
        context.Services.TryAddScoped<IAccountDeletionService, AspNetAccountDeletionService>();
        context.Services.TryAddScoped<IPasswordResetService, AspNetPasswordResetService>();
        context.Services.TryAddScoped<IPasskeyService, AspNetPasskeyService>();
        context.Services.TryAddScoped<IImpersonationService, AspNetImpersonationService>();

        // GranitUser implements IHasExtraProperties — apps can extend user properties
        // by calling AddExtraPropertyMappings<GranitUser> in their own module.
        // The ExtraPropertySyncInterceptor in Granit.Persistence handles sync automatically.
        context.Services.AddExtraPropertyInfrastructure();

        // Load signing/encryption keys from DB at startup (replaces ephemeral keys)
        context.Services.AddSingleton<IPostConfigureOptions<OpenIddictServerOptions>,
            DatabaseSigningKeyPostConfigure>();
    }
}
