using Granit.Core.MultiTenancy;
using Granit.Identity.EntityFrameworkCore.Internal;
using Granit.Identity.EntityFrameworkCore.Options;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.EntityFrameworkCore.Tests;

public sealed class EfCoreUserCacheStatsTests
{
    private readonly IUserCacheStore _store = Substitute.For<IUserCacheStore>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly EfCoreUserCacheStats _stats;

    public EfCoreUserCacheStatsTests()
    {
        UserCacheOptions options = new() { StalenessThreshold = TimeSpan.FromHours(24) };
        _stats = new EfCoreUserCacheStats(
            _store,
            _currentTenant,
            _timeProvider,
            Microsoft.Extensions.Options.Options.Create(options));
    }

    [Fact]
    public async Task GetCountAsync_DelegatesToStore_WithTenantId()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);
        _store.GetCountAsync(tenantId, Arg.Any<CancellationToken>()).Returns(42);

        int result = await _stats.GetCountAsync(TestContext.Current.CancellationToken);

        result.ShouldBe(42);
        await _store.Received(1).GetCountAsync(tenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCountAsync_NoTenant_PassesNull()
    {
        _currentTenant.IsAvailable.Returns(false);
        _store.GetCountAsync(null, Arg.Any<CancellationToken>()).Returns(10);

        int result = await _stats.GetCountAsync(TestContext.Current.CancellationToken);

        result.ShouldBe(10);
        await _store.Received(1).GetCountAsync(null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetStaleCountAsync_ComputesThreshold()
    {
        DateTimeOffset now = new(2026, 3, 21, 12, 0, 0, TimeSpan.Zero);
        _timeProvider.SetUtcNow(now);
        _currentTenant.IsAvailable.Returns(false);

        DateTimeOffset expectedThreshold = now - TimeSpan.FromHours(24);
        _store.GetStaleCountAsync(null, expectedThreshold, Arg.Any<CancellationToken>()).Returns(5);

        int result = await _stats.GetStaleCountAsync(TestContext.Current.CancellationToken);

        result.ShouldBe(5);
    }

    [Fact]
    public async Task GetSyncRangeAsync_DelegatesToStore()
    {
        _currentTenant.IsAvailable.Returns(false);
        DateTimeOffset oldest = DateTimeOffset.UtcNow.AddDays(-7);
        DateTimeOffset newest = DateTimeOffset.UtcNow;
        _store.GetSyncRangeAsync(null, Arg.Any<CancellationToken>()).Returns((oldest, newest));

        (DateTimeOffset? resultOldest, DateTimeOffset? resultNewest) =
            await _stats.GetSyncRangeAsync(TestContext.Current.CancellationToken);

        resultOldest.ShouldBe(oldest);
        resultNewest.ShouldBe(newest);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

        public void SetUtcNow(DateTimeOffset value) => _utcNow = value;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
