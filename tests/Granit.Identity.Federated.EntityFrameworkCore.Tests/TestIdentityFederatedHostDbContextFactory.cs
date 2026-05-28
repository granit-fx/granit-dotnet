using Granit.DataFiltering;
using Granit.Identity.Federated.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.Federated.EntityFrameworkCore.Tests;

/// <summary>
/// Test factory that opens fresh <see cref="IdentityFederatedHostDbContext"/> instances over a
/// shared <see cref="DbContextOptions{T}"/>. Each instance is wired with the supplied
/// <see cref="IDataFilter"/> so tests can opt out of the <c>IMultiTenant</c> filter
/// without setting up an ambient <c>ICurrentTenant</c>.
/// </summary>
internal sealed class TestIdentityFederatedHostDbContextFactory(
    DbContextOptions<IdentityFederatedHostDbContext> options,
    IDataFilter? dataFilter = null) : IDbContextFactory<IdentityFederatedHostDbContext>
{
    public IdentityFederatedHostDbContext CreateDbContext()
        => new(options, GranitDesignTime.CurrentTenant, dataFilter);

    public Task<IdentityFederatedHostDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IdentityFederatedHostDbContext>(new(options, GranitDesignTime.CurrentTenant, dataFilter));
}
