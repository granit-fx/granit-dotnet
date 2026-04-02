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
/// Granit module that registers EF Core persistence for OpenIddict.
/// </summary>
/// <remarks>
/// Registers <see cref="Internal.OpenIddictDbContext"/>, EF stores (groups, signing keys),
/// data seeding, extra-property infrastructure, and signing key loading at startup.
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

        // GranitUser implements IHasExtraProperties — apps can extend user properties
        // by calling AddExtraPropertyMappings<GranitUser> in their own module.
        // The ExtraPropertySyncInterceptor in Granit.Persistence handles sync automatically.
        context.Services.AddExtraPropertyInfrastructure();

        // Load signing/encryption keys from DB at startup (replaces ephemeral keys)
        context.Services.AddSingleton<IPostConfigureOptions<OpenIddictServerOptions>,
            DatabaseSigningKeyPostConfigure>();
    }
}
