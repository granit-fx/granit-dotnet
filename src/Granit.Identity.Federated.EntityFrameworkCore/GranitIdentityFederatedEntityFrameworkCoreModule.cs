using Granit.Modularity;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Identity.Federated.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core identity user cache persistence. Owns the dedicated
/// <c>IdentityFederatedDbContext</c> and replaces the default null stores with
/// <c>EfCoreUserCacheStore</c> / <c>EfCoreUserCacheStats</c>.
/// </summary>
/// <remarks>
/// Promoted out of the legacy interface-only pattern (<c>IUserCacheDbContext</c>) by
/// V2 of Epic #2382 — wire via
/// <c>builder.AddGranitIdentityFederatedEntityFrameworkCore(opts =&gt; opts.UseNpgsql(connectionString))</c>.
/// </remarks>
[DependsOn(
    typeof(GranitIdentityFederatedModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule),
    typeof(GranitMultiTenancyModule))]
public sealed class GranitIdentityFederatedEntityFrameworkCoreModule : GranitModule;
