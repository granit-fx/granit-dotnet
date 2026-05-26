using Granit.Indexing.Diagnostics;
using Granit.Indexing.Internal;
using Granit.Indexing.Options;
using Granit.MultiTenancy;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace Granit.Indexing.Tests;

public sealed class EmptyResultRateLimiterTests
{
    private static EmptyResultRateLimiter Build(int cap, FakeTimeProvider time)
    {
        TestMeterFactory meterFactory = new();
        return new EmptyResultRateLimiter(
            time,
            new GranitIndexingOptions { MaxEmptyResultQueriesPerPrincipalPerMinute = cap },
            new IndexingMetrics(meterFactory),
            NullTenantContext.Instance);
    }

    [Fact]
    public void Trips_on_eleventh_empty_query_when_cap_is_ten()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        EmptyResultRateLimiter limiter = Build(cap: 10, time);

        for (int i = 0; i < 10; i++)
        {
            limiter.RecordEmptyResultAndShouldThrottle("alice").ShouldBeFalse();
        }

        limiter.RecordEmptyResultAndShouldThrottle("alice").ShouldBeTrue();
    }

    [Fact]
    public void Window_slides_after_one_minute()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        EmptyResultRateLimiter limiter = Build(cap: 2, time);

        limiter.RecordEmptyResultAndShouldThrottle("bob").ShouldBeFalse();
        limiter.RecordEmptyResultAndShouldThrottle("bob").ShouldBeFalse();
        limiter.RecordEmptyResultAndShouldThrottle("bob").ShouldBeTrue();

        time.Advance(TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(1)));

        // All prior timestamps are now outside the trailing-minute window.
        limiter.RecordEmptyResultAndShouldThrottle("bob").ShouldBeFalse();
    }

    [Fact]
    public void Buckets_are_per_principal()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        EmptyResultRateLimiter limiter = Build(cap: 1, time);

        limiter.RecordEmptyResultAndShouldThrottle("alice").ShouldBeFalse();
        limiter.RecordEmptyResultAndShouldThrottle("alice").ShouldBeTrue();

        // Bob has a clean slate.
        limiter.RecordEmptyResultAndShouldThrottle("bob").ShouldBeFalse();
    }
}
