using Granit.DataFiltering;
using Granit.Identity.Federated.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.Federated.EntityFrameworkCore.Internal;

/// <summary>
/// Tenant-isolated EF Core DbContext for the Granit.Identity.Federated user-cache under
/// <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode.Segregated"/>.
/// </summary>
/// <remarks>
/// Holds tenant-federated identities in each tenant's own schema (under
/// <c>SchemaPerTenant</c>) or database (under <c>DatabasePerTenant</c>). Per-tenant SSO
/// revocation by <c>DROP SCHEMA &lt;tenant&gt; CASCADE</c> is the GDPR Art. 17 primitive
/// enabled by this layout. The companion host-side context is
/// <see cref="IdentityFederatedHostDbContext"/>.
/// </remarks>
internal sealed class IdentityFederatedTenantDbContext(
    DbContextOptions<IdentityFederatedTenantDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter), IIdentityFederatedDbContext
{
    /// <inheritdoc/>
    public DbSet<FederatedIdentity> FederatedIdentities => Set<FederatedIdentity>();

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ConfigureIdentityModule();
}
