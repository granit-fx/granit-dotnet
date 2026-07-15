using Granit.DataFiltering;
using Granit.Identity.Federated.EntityFrameworkCore.Internal;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.Federated.EntityFrameworkCore.Tests;

/// <summary>
/// Test factory that opens fresh <see cref="IdentityFederatedDbContext"/> instances over a
/// shared <see cref="DbContextOptions{T}"/>. Pass an <see cref="ICurrentTenant"/> (e.g. a
/// <c>FakeCurrentTenant</c>) to exercise the <c>IMultiTenant</c> query filter the way
/// production scopes do; pass an <see cref="IDataFilter"/> only to opt out of the filter
/// entirely (legacy tests).
/// </summary>
internal sealed class TestIdentityFederatedDbContextFactory(
    DbContextOptions<IdentityFederatedDbContext> options,
    IDataFilter? dataFilter = null,
    ICurrentTenant? currentTenant = null) : IDbContextFactory<IdentityFederatedDbContext>
{
    public IdentityFederatedDbContext CreateDbContext()
        => new(options, currentTenant ?? GranitDesignTime.CurrentTenant, dataFilter);

    public Task<IdentityFederatedDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IdentityFederatedDbContext>(
            new(options, currentTenant ?? GranitDesignTime.CurrentTenant, dataFilter));
}
