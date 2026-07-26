using Granit.DataExchange;
using Granit.Identity.Local;
using Granit.Identity.Local.EntityFrameworkCore;
using Granit.Modularity;
using Granit.MultiTenancy;
using Granit.OpenIddict.EntityFrameworkCore.Internal;
using Granit.OpenIddict.EntityFrameworkCore.Seeding;
using Granit.OpenIddict.Services;
using Granit.Persistence.DataSeeding;
using Granit.Persistence.EntityFrameworkCore;
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
    typeof(GranitIdentityLocalEntityFrameworkCoreModule),
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

        // EF Core stores. The account-deletion / registration reconciliation stores read
        // LocalIdentity from IdentityLocalDbContext (sibling package); the signing-key and
        // session-activity stores read the OpenIddict-owned tables from OpenIddictDbContext.
        context.Services.TryAddScoped<ISigningKeyStore, EfSigningKeyStore>();
        context.Services.TryAddScoped<IPendingAccountDeletionStore, EfPendingAccountDeletionStore>();
        context.Services.TryAddScoped<IPendingRegistrationStore, EfPendingRegistrationStore>();
        context.Services.TryAddScoped<IUserSessionActivityStore, EfUserSessionActivityStore>();
    }
}
