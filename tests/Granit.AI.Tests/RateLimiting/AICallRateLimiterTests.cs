using Granit.AI.RateLimiting;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Granit.AI.Tests;

public sealed class AICallRateLimiterTests
{
    [Fact]
    public async Task TryAcquire_admits_calls_up_to_the_cap()
    {
        AICallRateLimiter limiter = new(TimeProvider.System);

        for (int i = 0; i < 5; i++)
        {
            (await limiter.TryAcquireAsync("bucket", maxCallsPerHour: 5, TestContext.Current.CancellationToken)).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task TryAcquire_denies_the_call_immediately_after_the_cap()
    {
        AICallRateLimiter limiter = new(TimeProvider.System);
        for (int i = 0; i < 3; i++)
        {
            await limiter.TryAcquireAsync("bucket", maxCallsPerHour: 3, TestContext.Current.CancellationToken);
        }

        (await limiter.TryAcquireAsync("bucket", maxCallsPerHour: 3, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Cap_is_per_bucket_so_tenants_dont_share_quota()
    {
        // Different bucket keys (typically per-tenant) MUST have isolated windows —
        // otherwise a noisy-neighbor tenant would starve the rest of the platform.
        AICallRateLimiter limiter = new(TimeProvider.System);

        await limiter.TryAcquireAsync("tenant-a", maxCallsPerHour: 1, TestContext.Current.CancellationToken);
        (await limiter.TryAcquireAsync("tenant-a", maxCallsPerHour: 1, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await limiter.TryAcquireAsync("tenant-b", maxCallsPerHour: 1, TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task Window_reopens_one_hour_after_the_oldest_timestamp()
    {
        // Sliding window: as soon as the oldest in-window call expires, the bucket
        // accepts a new one. Tests this via FakeTimeProvider so the assertion is
        // deterministic, not wall-clock-dependent.
        FakeTimeProvider clock = new();
        AICallRateLimiter limiter = new(clock);

        (await limiter.TryAcquireAsync("bucket", maxCallsPerHour: 1, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await limiter.TryAcquireAsync("bucket", maxCallsPerHour: 1, TestContext.Current.CancellationToken)).ShouldBeFalse();

        clock.Advance(TimeSpan.FromMinutes(61));

        (await limiter.TryAcquireAsync("bucket", maxCallsPerHour: 1, TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task TryAcquire_rejects_null_or_empty_bucket_key()
    {
        AICallRateLimiter limiter = new(TimeProvider.System);

        await Should.ThrowAsync<ArgumentException>(async () =>
            await limiter.TryAcquireAsync(bucketKey: "", maxCallsPerHour: 1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryAcquire_rejects_non_positive_cap()
    {
        AICallRateLimiter limiter = new(TimeProvider.System);

        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
            await limiter.TryAcquireAsync("bucket", maxCallsPerHour: 0, TestContext.Current.CancellationToken));
    }
}
