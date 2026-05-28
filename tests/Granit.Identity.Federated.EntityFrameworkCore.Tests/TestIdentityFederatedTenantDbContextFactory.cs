using Granit.DataFiltering;
using Granit.Identity.Federated.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.Federated.EntityFrameworkCore.Tests;

/// <summary>
/// Test factory mirroring <see cref="TestIdentityFederatedHostDbContextFactory"/> for the
/// Segregated-mode tenant context. Used by <see cref="SegregatedDispatchTests"/> to seed
/// rows in the per-tenant DB independently of the host DB.
/// </summary>
internal sealed class TestIdentityFederatedTenantDbContextFactory(
    DbContextOptions<IdentityFederatedTenantDbContext> options,
    IDataFilter? dataFilter = null) : IDbContextFactory<IdentityFederatedTenantDbContext>
{
    public IdentityFederatedTenantDbContext CreateDbContext()
        => new(options, GranitDesignTime.CurrentTenant, dataFilter);

    public Task<IdentityFederatedTenantDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IdentityFederatedTenantDbContext>(new(options, GranitDesignTime.CurrentTenant, dataFilter));
}
