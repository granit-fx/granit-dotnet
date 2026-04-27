using Granit.CustomerBalance.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.CustomerBalance.EntityFrameworkCore.Tests.Integration;

internal sealed class TestCustomerBalanceDbContextFactory : IDbContextFactory<CustomerBalanceDbContext>, IAsyncDisposable
{
    private readonly DbContextOptions<CustomerBalanceDbContext> _options;

    public TestCustomerBalanceDbContextFactory(string connectionString)
    {
        _options = new DbContextOptionsBuilder<CustomerBalanceDbContext>()
            .UseNpgsql(connectionString)
            .Options;
    }

    public CustomerBalanceDbContext CreateDbContext() => new(_options);

    public Task<CustomerBalanceDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new CustomerBalanceDbContext(_options));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
