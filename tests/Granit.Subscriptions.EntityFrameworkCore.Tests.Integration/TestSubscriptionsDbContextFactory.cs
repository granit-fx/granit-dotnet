using Granit.Subscriptions.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Subscriptions.EntityFrameworkCore.Tests.Integration;

internal sealed class TestSubscriptionsDbContextFactory : IDbContextFactory<SubscriptionsDbContext>, IAsyncDisposable
{
    private readonly DbContextOptions<SubscriptionsDbContext> _options;

    public TestSubscriptionsDbContextFactory(string connectionString)
    {
        _options = new DbContextOptionsBuilder<SubscriptionsDbContext>()
            .UseNpgsql(connectionString)
            .Options;
    }

    public SubscriptionsDbContext CreateDbContext() => new(_options);

    public Task<SubscriptionsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new SubscriptionsDbContext(_options));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
