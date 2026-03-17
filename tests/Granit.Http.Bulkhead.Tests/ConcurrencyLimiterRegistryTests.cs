using System.Threading.RateLimiting;
using Granit.Http.Bulkhead.Internal;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace Granit.Http.Bulkhead.Tests;

public sealed class ConcurrencyLimiterRegistryTests : IDisposable
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly ConcurrencyLimiterRegistry _registry;

    public ConcurrencyLimiterRegistryTests()
    {
        _registry = new ConcurrencyLimiterRegistry(_timeProvider);
    }

    public void Dispose() => _registry.Dispose();

    // =========================================================================
    // Acquire / Release
    // =========================================================================

    [Fact]
    public async Task AcquireAsync_ReturnsAcquiredLease()
    {
        RateLimitLease lease = await _registry.AcquireAsync("api:tenant1", permitLimit: 5, queueLimit: 0, TestContext.Current.CancellationToken);

        lease.IsAcquired.ShouldBeTrue();
        lease.Dispose();
    }

    [Fact]
    public async Task AcquireAsync_MultipleCalls_SameKey_SharesLimiter()
    {
        RateLimitLease lease1 = await _registry.AcquireAsync("api:tenant1", permitLimit: 2, queueLimit: 0, TestContext.Current.CancellationToken);
        RateLimitLease lease2 = await _registry.AcquireAsync("api:tenant1", permitLimit: 2, queueLimit: 0, TestContext.Current.CancellationToken);

        lease1.IsAcquired.ShouldBeTrue();
        lease2.IsAcquired.ShouldBeTrue();
        _registry.Count.ShouldBe(1);

        lease1.Dispose();
        lease2.Dispose();
    }

    // =========================================================================
    // Two tenants are independent
    // =========================================================================

    [Fact]
    public async Task AcquireAsync_DifferentKeys_IndependentLimiters()
    {
        // Fill tenant1's limiter (permitLimit=1)
        RateLimitLease lease1 = await _registry.AcquireAsync("api:tenant1", permitLimit: 1, queueLimit: 0, TestContext.Current.CancellationToken);

        // tenant2 should still be able to acquire
        RateLimitLease lease2 = await _registry.AcquireAsync("api:tenant2", permitLimit: 1, queueLimit: 0, TestContext.Current.CancellationToken);

        lease1.IsAcquired.ShouldBeTrue();
        lease2.IsAcquired.ShouldBeTrue();
        _registry.Count.ShouldBe(2);

        lease1.Dispose();
        lease2.Dispose();
    }

    // =========================================================================
    // Rejection when full
    // =========================================================================

    [Fact]
    public async Task AcquireAsync_WhenFull_ReturnsNotAcquired()
    {
        RateLimitLease lease1 = await _registry.AcquireAsync("api:tenant1", permitLimit: 1, queueLimit: 0, TestContext.Current.CancellationToken);

        // Second acquire should fail (no queue)
        RateLimitLease lease2 = await _registry.AcquireAsync("api:tenant1", permitLimit: 1, queueLimit: 0, TestContext.Current.CancellationToken);

        lease1.IsAcquired.ShouldBeTrue();
        lease2.IsAcquired.ShouldBeFalse();

        lease1.Dispose();
        lease2.Dispose();
    }

    [Fact]
    public async Task AcquireAsync_AfterRelease_CanAcquireAgain()
    {
        RateLimitLease lease1 = await _registry.AcquireAsync("api:tenant1", permitLimit: 1, queueLimit: 0, TestContext.Current.CancellationToken);
        lease1.Dispose();

        RateLimitLease lease2 = await _registry.AcquireAsync("api:tenant1", permitLimit: 1, queueLimit: 0, TestContext.Current.CancellationToken);

        lease2.IsAcquired.ShouldBeTrue();
        lease2.Dispose();
    }

    // =========================================================================
    // Eviction
    // =========================================================================

    [Fact]
    public async Task EvictIdle_RemovesIdleLimiters()
    {
        RateLimitLease lease = await _registry.AcquireAsync("api:tenant1", permitLimit: 5, queueLimit: 0, TestContext.Current.CancellationToken);
        lease.Dispose();

        _timeProvider.Advance(TimeSpan.FromMinutes(31));

        int evicted = _registry.EvictIdle(TimeSpan.FromMinutes(30));

        evicted.ShouldBe(1);
        _registry.Count.ShouldBe(0);
    }

    [Fact]
    public async Task EvictIdle_KeepsRecentlyUsedLimiters()
    {
        RateLimitLease lease = await _registry.AcquireAsync("api:tenant1", permitLimit: 5, queueLimit: 0, TestContext.Current.CancellationToken);
        lease.Dispose();

        _timeProvider.Advance(TimeSpan.FromMinutes(10));

        int evicted = _registry.EvictIdle(TimeSpan.FromMinutes(30));

        evicted.ShouldBe(0);
        _registry.Count.ShouldBe(1);
    }

    [Fact]
    public async Task EvictIdle_RecentUsageResetsIdleTimer()
    {
        RateLimitLease lease1 = await _registry.AcquireAsync("api:tenant1", permitLimit: 5, queueLimit: 0, TestContext.Current.CancellationToken);
        lease1.Dispose();

        _timeProvider.Advance(TimeSpan.FromMinutes(25));

        // Use it again — resets the idle timer
        RateLimitLease lease2 = await _registry.AcquireAsync("api:tenant1", permitLimit: 5, queueLimit: 0, TestContext.Current.CancellationToken);
        lease2.Dispose();

        _timeProvider.Advance(TimeSpan.FromMinutes(10));

        int evicted = _registry.EvictIdle(TimeSpan.FromMinutes(30));

        evicted.ShouldBe(0);
        _registry.Count.ShouldBe(1);
    }

    // =========================================================================
    // Dispose
    // =========================================================================

    [Fact]
    public async Task Dispose_ClearsAllLimiters()
    {
        RateLimitLease lease = await _registry.AcquireAsync("api:tenant1", permitLimit: 5, queueLimit: 0, TestContext.Current.CancellationToken);
        lease.Dispose();

        _registry.Dispose();

        _registry.Count.ShouldBe(0);
    }
}
