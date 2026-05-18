using Granit.DataExchange.EntityFrameworkCore.Internal;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.DataExchange.EntityFrameworkCore.Tests.Infrastructure;

/// <summary>
/// InMemory factory for <see cref="DataExchangeDbContext"/>.
/// Each instance uses the same named database for test isolation.
/// Accepts an <see cref="ICurrentTenant"/> so the DbContext-side
/// <see cref="Granit.Domain.IMultiTenant"/> query filter sees the same tenant
/// as the store that consumes the factory.
/// </summary>
internal sealed class InMemoryDataExchangeContextFactory(string dbName, ICurrentTenant? tenant = null)
    : IDbContextFactory<DataExchangeDbContext>
{
    private readonly ICurrentTenant _tenant = tenant ?? GranitDesignTime.CurrentTenant;

    public DataExchangeDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<DataExchangeDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options,
            _tenant);

    public Task<DataExchangeDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateDbContext());
}
