using Granit.DataFiltering;
using Granit.Identity.Federated.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.Federated.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core DbContext owning the Granit.Identity.Federated user-cache.
/// </summary>
/// <remarks>
/// <para>
/// Dual-scope module: host-federated identities (host admins via the IDP) and
/// tenant-federated identities coexist in the host schema with row-level tenant filtering
/// on <see cref="FederatedIdentity.TenantId"/>. Tables live in
/// <see cref="GranitDbDefaults.HostDbSchema"/> (see <see cref="GranitIdentityDbProperties.DbSchema"/>).
/// </para>
/// <para>
/// Registered via <c>AddGranitDbContext&lt;IdentityFederatedDbContext&gt;</c> by
/// <see cref="Extensions.IdentityFederatedEntityFrameworkCoreHostApplicationBuilderExtensions
/// .AddGranitIdentityFederatedEntityFrameworkCore"/>. Replaces the legacy
/// <c>IUserCacheDbContext</c> interface-only pattern that predated
/// <c>AddGranitIsolatedDbContext</c>.
/// </para>
/// </remarks>
internal sealed class IdentityFederatedDbContext(
    DbContextOptions<IdentityFederatedDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    /// <summary>Federated identities (per-IDP user cache).</summary>
    public DbSet<FederatedIdentity> FederatedIdentities => Set<FederatedIdentity>();

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ConfigureIdentityModule();
}
