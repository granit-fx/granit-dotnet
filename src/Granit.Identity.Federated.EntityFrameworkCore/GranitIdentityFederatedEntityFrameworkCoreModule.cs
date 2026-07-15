using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Identity.Federated.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core identity user cache persistence. Owns the dedicated
/// <c>IdentityFederatedDbContext</c> and replaces the default null stores with
/// <c>EfCoreUserCacheStore</c> / <c>UserCacheStats</c>.
/// </summary>
/// <remarks>
/// <para>
/// Promoted out of the legacy interface-only pattern (<c>IUserCacheDbContext</c>) by
/// V2 of Epic #2382 — wire via
/// <c>builder.AddGranitIdentityFederatedEntityFrameworkCore(opts =&gt; opts.UseNpgsql(connectionString))</c>.
/// </para>
/// <para>
/// <c>Granit.MultiTenancy</c> is intentionally NOT a hard dependency: single-tenant
/// deployments (e.g. an SSO-only static site) can consume this module without pulling
/// the multi-tenant infrastructure. <c>ICurrentTenant</c> resolves to
/// <c>NullTenantContext</c> by default and the row-level <c>IMultiTenant</c> filter
/// keeps working — rows with <c>TenantId == null</c> remain visible. The Segregated
/// mode (Phase B) that needs <c>ITenantReader</c> will ship in a dedicated companion
/// package to keep this dependency boundary clean.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitIdentityFederatedModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitIdentityFederatedEntityFrameworkCoreModule : GranitModule;
