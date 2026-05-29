using Granit.DataFiltering;
using Granit.Identity.Federated.Domain;
using Granit.Identity.Federated.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.Federated.EntityFrameworkCore.Internal;

/// <summary>
/// Host-pinned EF Core DbContext for the Granit.Identity.Federated user-cache.
/// </summary>
/// <remarks>
/// <para>
/// Under <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode.Shared"/> this
/// is the single context that holds every federated identity (host + tenant rows) with
/// the row-level <c>IMultiTenant</c> filter active. Under
/// <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode.Segregated"/> this
/// context holds <i>only</i> host-federated identities (host admins via the IDP);
/// tenant-federated identities live in the companion
/// <see cref="IdentityFederatedTenantDbContext"/>.
/// </para>
/// <para>
/// Registered via <c>AddGranitDbContext&lt;IdentityFederatedHostDbContext&gt;</c>.
/// Tables live in <see cref="GranitDbDefaults.HostDbSchema"/>.
/// </para>
/// </remarks>
internal sealed class IdentityFederatedHostDbContext(
    DbContextOptions<IdentityFederatedHostDbContext> options,
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
