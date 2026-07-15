using Granit.Identity.Federated.Domain;
using Granit.Identity.Federated.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntityFrameworkCore.Tests;

/// <summary>
/// Staleness-diagnostics tests kept on the EF Core In-Memory provider: SQLite cannot
/// translate comparisons or Min/Max aggregates on <see cref="DateTimeOffset"/> columns
/// (<c>LastSyncedAt</c>), which PostgreSQL — the production provider — handles natively.
/// Only pure host-scope diagnostics live here; everything with tenant-filter semantics is
/// covered by the SQLite-backed <see cref="EfCoreUserCacheStoreTests"/>.
/// </summary>
public sealed class EfCoreUserCacheStoreDiagnosticsTests : IDisposable
{
    private readonly TestDataFilter _filter = new();

    public void Dispose() => _filter.Dispose();

    private EfCoreUserCacheStore CreateStore()
    {
        DbContextOptions<IdentityFederatedDbContext> options = new DbContextOptionsBuilder<IdentityFederatedDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        TestIdentityFederatedDbContextFactory factory = new(options, _filter.Filter);

        IUserLookupHasher hasher = Substitute.For<IUserLookupHasher>();
        hasher.ComputeEmailHash(Arg.Any<string?>()).Returns((string?)null);
        return new EfCoreUserCacheStore(factory, hasher);
    }

    private static FederatedIdentity CreateEntry(string externalUserId, DateTimeOffset lastSyncedAt) => new()
    {
        Id = Guid.NewGuid(),
        ExternalUserId = externalUserId,
        Username = "jdoe",
        Email = null,
        Enabled = true,
        LastSyncedAt = lastSyncedAt,
        TenantId = null
    };

    [Fact]
    public async Task GetStaleCountAsync_CountsStaleEntries()
    {
        EfCoreUserCacheStore store = CreateStore();

        await store.UpsertAsync(CreateEntry("user-fresh", DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
        await store.UpsertAsync(CreateEntry("user-stale", DateTimeOffset.UtcNow.AddDays(-2)), TestContext.Current.CancellationToken);

        DateTimeOffset threshold = DateTimeOffset.UtcNow.AddDays(-1);
        int staleCount = await store.GetStaleCountAsync(null, threshold, TestContext.Current.CancellationToken);

        staleCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetSyncRangeAsync_ReturnsOldestAndNewest()
    {
        EfCoreUserCacheStore store = CreateStore();

        await store.UpsertAsync(
            CreateEntry("user-old", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            TestContext.Current.CancellationToken);
        await store.UpsertAsync(
            CreateEntry("user-recent", new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero)),
            TestContext.Current.CancellationToken);

        (DateTimeOffset? oldest, DateTimeOffset? newest) = await store.GetSyncRangeAsync(
            null, TestContext.Current.CancellationToken);

        oldest.ShouldBe(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        newest.ShouldBe(new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero));
    }
}
