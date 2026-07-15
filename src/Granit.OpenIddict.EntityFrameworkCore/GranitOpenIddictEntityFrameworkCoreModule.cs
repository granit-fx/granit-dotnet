using Granit.DataExchange;
using Granit.Encryption;
using Granit.Identity.Local;
using Granit.Identity.Local.Services;
using Granit.Modularity;
using Granit.MultiTenancy;
using Granit.OpenIddict.EntityFrameworkCore.Internal;
using Granit.OpenIddict.EntityFrameworkCore.Seeding;
using Granit.OpenIddict.Services;
using Granit.Persistence.DataSeeding;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.OpenIddict.EntityFrameworkCore;

/// <summary>
/// Granit module that registers EF Core persistence for OpenIddict.
/// </summary>
/// <remarks>
/// Registers <see cref="Internal.OpenIddictDbContext"/>, EF stores (groups, signing keys),
/// data seeding, and extra-property infrastructure.
/// </remarks>
[DependsOn(
    typeof(GranitDataExchangeModule),
    typeof(GranitEncryptionModule),
    typeof(GranitIdentityLocalModule),
    typeof(GranitMultiTenancyModule),
    typeof(GranitOpenIddictModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitOpenIddictEntityFrameworkCoreModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Data seeding (host-level: OIDC apps and scopes are global)
        context.Services.AddTransient<IHostDataSeedContributor, OpenIddictSeedContributor>();

        // EF Core stores
        context.Services.TryAddScoped<ILocalIdentityGroupStore, OpenIddictGroupStore>();
        context.Services.TryAddScoped<ISigningKeyStore, EfSigningKeyStore>();
        context.Services.TryAddScoped<IPendingAccountDeletionStore, EfPendingAccountDeletionStore>();
        context.Services.TryAddScoped<IPendingRegistrationStore, EfPendingRegistrationStore>();
        context.Services.TryAddScoped<IUserSessionActivityStore, EfUserSessionActivityStore>();

        // LocalIdentity implements IHasMetadata — apps can extend user properties
        // by calling AddMetadataMappings<LocalIdentity> in their own module.
        // AddMetadataInfrastructure registers MetadataSyncInterceptor as IGranitAutoInterceptor
        // so UseGranitInterceptors picks it up on every DbContext automatically.
        context.Services.AddMetadataInfrastructure();
    }
}
