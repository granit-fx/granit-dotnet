// =============================================================================
// Tests - ConcurrencyLimiterRegistry (real contention)
// =============================================================================
// The registry exists to provide thread safety under parallel load; these tests
// exercise it with genuine concurrency (Task.WhenAll / Parallel.ForEachAsync),
// not FakeTimeProvider-sequenced logic. Complements the sequential specs in
// ConcurrencyLimiterRegistryTests.
// =============================================================================

using System.Diagnostics.Metrics;
using System.Threading.RateLimiting;
using Granit.Bulkhead.Diagnostics;
using Granit.Bulkhead.Options;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Bulkhead.Tests;

public sealed class ConcurrencyLimiterRegistryContentionTests
{
    private static ConcurrencyLimiterRegistry CreateRegistry(int maxLimiters = 10_000)
    {
        ServiceProvider sp = new ServiceCollection().AddMetrics().BuildServiceProvider();
        return new ConcurrencyLimiterRegistry(
            TimeProvider.System,
            Microsoft.Extensions.Options.Options.Create(new GranitBulkheadOptions { MaxLimiters = maxLimiters }),
            new BulkheadMetrics(sp.GetRequiredService<IMeterFactory>()));
    }

    [Fact]
    public async Task ParallelAcquireRelease_OnSameKey_NeverExceedsPermitLimit()
    {
        const int permitLimit = 4;
        const int workers = 64;
        using ConcurrencyLimiterRegistry registry = CreateRegistry();

        int concurrent = 0;
        int observedMax = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, workers),
            new ParallelOptions { MaxDegreeOfParallelism = workers },
            async (_, ct) =>
            {
                using RateLimitLease lease = await registry.AcquireAsync(
                    "policy:tenant", permitLimit, queueLimit: workers, ct);
                if (!lease.IsAcquired)
                {
                    return;
                }

                int now = Interlocked.Increment(ref concurrent);
                InterlockedMax(ref observedMax, now);
                await Task.Delay(5, ct);
                Interlocked.Decrement(ref concurrent);
            });

        observedMax.ShouldBeLessThanOrEqualTo(permitLimit);
        observedMax.ShouldBeGreaterThan(1, "the test must actually run concurrently to prove anything");
    }

    [Fact]
    public async Task UniqueKeyBurst_UnderParallelLoad_NeverGrowsPastMaxLimiters()
    {
        const int maxLimiters = 32;
        const int burst = 512;
        using ConcurrencyLimiterRegistry registry = CreateRegistry(maxLimiters);

        await Parallel.ForEachAsync(
            Enumerable.Range(0, burst),
            new ParallelOptions { MaxDegreeOfParallelism = 32 },
            async (i, ct) =>
            {
                using RateLimitLease lease = await registry.AcquireAsync(
                    $"policy:tenant-{i}", permitLimit: 2, queueLimit: 0, ct);
                // Release immediately — the point is registry growth, not permits.
            });

        registry.Count.ShouldBeLessThanOrEqualTo(maxLimiters);
    }

    [Fact]
    public async Task EvictIdle_UnderParallelAcquires_NeverDisposesLimitersWithOutstandingLeases()
    {
        using ConcurrencyLimiterRegistry registry = CreateRegistry();

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        var evictor = Task.Run(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                // Zero idle-timeout: every entry is a candidate; only the
                // outstanding-work guard protects in-flight leases.
                registry.EvictIdle(TimeSpan.Zero);
            }
        }, CancellationToken.None);

        // ObjectDisposedException on lease release would surface here if the
        // evictor ever disposed a limiter that still had an acquired permit.
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 256),
            new ParallelOptions { MaxDegreeOfParallelism = 16 },
            async (i, ct) =>
            {
                using RateLimitLease lease = await registry.AcquireAsync(
                    $"policy:tenant-{i % 8}", permitLimit: 2, queueLimit: 64, ct);
                await Task.Delay(1, ct);
            });

        await cts.CancelAsync();
        await evictor;
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int snapshot;
        while (value > (snapshot = Volatile.Read(ref target))
               && Interlocked.CompareExchange(ref target, value, snapshot) != snapshot)
        {
        }
    }
}
