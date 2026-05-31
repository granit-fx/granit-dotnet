using Granit.DataFiltering;
using Granit.Identity.Federated.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.Federated.EntityFrameworkCore.Tests;

/// <summary>
/// Test factory that opens fresh <see cref="IdentityFederatedDbContext"/> instances over a
/// shared <see cref="DbContextOptions{T}"/>. Each instance is wired with the supplied
/// <see cref="IDataFilter"/> so tests can opt out of the <c>IMultiTenant</c> filter
/// without setting up an ambient <c>ICurrentTenant</c>.
/// </summary>
internal sealed class TestIdentityFederatedDbContextFactory(
    DbContextOptions<IdentityFederatedDbContext> options,
    IDataFilter? dataFilter = null) : IDbContextFactory<IdentityFederatedDbContext>
{
    public IdentityFederatedDbContext CreateDbContext()
        => new(options, GranitDesignTime.CurrentTenant, dataFilter);

    public Task<IdentityFederatedDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IdentityFederatedDbContext>(new(options, GranitDesignTime.CurrentTenant, dataFilter));
}
