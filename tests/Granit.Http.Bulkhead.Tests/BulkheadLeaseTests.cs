using System.Diagnostics.Metrics;
using System.Threading.RateLimiting;
using Granit.Http.Bulkhead.Abstractions;
using Granit.Http.Bulkhead.Diagnostics;
using Granit.Http.Bulkhead.Options;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Http.Bulkhead.Tests;

public sealed class BulkheadLeaseTests : IDisposable
{
    private readonly ConcurrencyLimiterRegistry _registry;

    public BulkheadLeaseTests()
    {
        ServiceProvider sp = new ServiceCollection().AddMetrics().BuildServiceProvider();
        BulkheadMetrics metrics = new(sp.GetRequiredService<IMeterFactory>());
        _registry = new ConcurrencyLimiterRegistry(
            TimeProvider.System,
            Microsoft.Extensions.Options.Options.Create(new GranitBulkheadOptions()),
            metrics);
    }

    public void Dispose() => _registry.Dispose();

    // =========================================================================
    // NoOp
    // =========================================================================

    [Fact]
    public void NoOp_IsAcquired_ReturnsTrue() =>
        BulkheadLease.NoOp.IsAcquired.ShouldBeTrue();

    [Fact]
    public void NoOp_Dispose_DoesNotThrow() =>
        Should.NotThrow(() =>
        {
            BulkheadLease.NoOp.Dispose();
            BulkheadLease.NoOp.Dispose();
        });

    // =========================================================================
    // Dispose releases permit
    // =========================================================================

    [Fact]
    public async Task Dispose_ReleasesPermitBackToRegistry()
    {
        // Acquire the only permit
        RateLimitLease innerLease = await _registry.AcquireAsync("test:t1", permitLimit: 1, queueLimit: 0, TestContext.Current.CancellationToken);
        innerLease.IsAcquired.ShouldBeTrue();

        bool callbackInvoked = false;
        var lease = new BulkheadLease(innerLease, () => callbackInvoked = true);

        lease.Dispose();

        callbackInvoked.ShouldBeTrue();

        // After disposing, the permit should be available again
        RateLimitLease secondLease = await _registry.AcquireAsync("test:t1", permitLimit: 1, queueLimit: 0, TestContext.Current.CancellationToken);
        secondLease.IsAcquired.ShouldBeTrue();
        secondLease.Dispose();
    }

    // =========================================================================
    // Double dispose is safe
    // =========================================================================

    [Fact]
    public async Task Dispose_CalledTwice_CallbackInvokedOnlyOnce()
    {
        RateLimitLease innerLease = await _registry.AcquireAsync("test:t1", permitLimit: 1, queueLimit: 0, TestContext.Current.CancellationToken);

        int callbackCount = 0;
        var lease = new BulkheadLease(innerLease, () => Interlocked.Increment(ref callbackCount));

        lease.Dispose();
        lease.Dispose();

        callbackCount.ShouldBe(1);
    }

    // =========================================================================
    // IsAcquired reflects inner lease
    // =========================================================================

    [Fact]
    public async Task IsAcquired_WhenInnerLeaseAcquired_ReturnsTrue()
    {
        RateLimitLease innerLease = await _registry.AcquireAsync("test:t1", permitLimit: 1, queueLimit: 0, TestContext.Current.CancellationToken);

        var lease = new BulkheadLease(innerLease, null);

        lease.IsAcquired.ShouldBeTrue();
        lease.Dispose();
    }

    [Fact]
    public async Task IsAcquired_WhenInnerLeaseNotAcquired_ReturnsFalse()
    {
        // Fill the limiter first
        RateLimitLease held = await _registry.AcquireAsync("test:t1", permitLimit: 1, queueLimit: 0, TestContext.Current.CancellationToken);

        // This one should not be acquired
        RateLimitLease innerLease = await _registry.AcquireAsync("test:t1", permitLimit: 1, queueLimit: 0, TestContext.Current.CancellationToken);

        var lease = new BulkheadLease(innerLease, null);

        lease.IsAcquired.ShouldBeFalse();

        lease.Dispose();
        held.Dispose();
    }
}
