using Granit.Identity.Federated.Internal;
using Granit.Identity.Federated.Options;
using Granit.MultiTenancy;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Tests.Internal;

public sealed class UserCacheStatsTests
{
    private readonly IUserCacheStore _store = Substitute.For<IUserCacheStore>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly UserCacheStats _stats;

    public UserCacheStatsTests()
    {
        UserCacheOptions options = new() { StalenessThreshold = TimeSpan.FromHours(24) };
        _stats = new UserCacheStats(
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
        _timeProvider.GetUtcNow().Returns(now);
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

}
