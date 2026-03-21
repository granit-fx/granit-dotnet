using Granit.RateLimiting.Abstractions;
using Granit.RateLimiting.Internal;
using Granit.RateLimiting.Options;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace Granit.RateLimiting.Tests;

public sealed class InMemoryRateLimitCounterStoreAdditionalTests
{
    private readonly FakeTimeProvider _timeProvider = new(DateTimeOffset.UtcNow);
    private readonly InMemoryRateLimitCounterStore _store;

    public InMemoryRateLimitCounterStoreAdditionalTests()
    {
        _store = new InMemoryRateLimitCounterStore(_timeProvider);
    }

    [Fact]
    public async Task UnknownAlgorithm_ReturnsAllowed()
    {
        RateLimitPolicyOptions policy = new() { PermitLimit = 100 };

        RateLimitResult result = await _store.CheckAndIncrementAsync(
            "key", 100, TimeSpan.FromMinutes(1), (RateLimitAlgorithm)99,
            policy, TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeTrue();
        result.Remaining.ShouldBe(100);
        result.Limit.ShouldBe(100);
        result.RetryAfter.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task SlidingWindow_ExactlyAtLimit_IsRejected()
    {
        RateLimitPolicyOptions policy = new() { PermitLimit = 1, Window = TimeSpan.FromMinutes(1) };

        await _store.CheckAndIncrementAsync(
            "key", 1, TimeSpan.FromMinutes(1), RateLimitAlgorithm.SlidingWindow,
            policy, TestContext.Current.CancellationToken);

        RateLimitResult result = await _store.CheckAndIncrementAsync(
            "key", 1, TimeSpan.FromMinutes(1), RateLimitAlgorithm.SlidingWindow,
            policy, TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.Remaining.ShouldBe(0);
    }

    [Fact]
    public async Task FixedWindow_DifferentKeys_TrackSeparately()
    {
        RateLimitPolicyOptions policy = new() { PermitLimit = 1, Window = TimeSpan.FromMinutes(1) };

        await _store.CheckAndIncrementAsync(
            "key1", 1, TimeSpan.FromMinutes(1), RateLimitAlgorithm.FixedWindow,
            policy, TestContext.Current.CancellationToken);

        RateLimitResult result = await _store.CheckAndIncrementAsync(
            "key2", 1, TimeSpan.FromMinutes(1), RateLimitAlgorithm.FixedWindow,
            policy, TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeTrue();
    }

    [Fact]
    public async Task TokenBucket_DifferentKeys_TrackSeparately()
    {
        RateLimitPolicyOptions policy = new()
        {
            Algorithm = RateLimitAlgorithm.TokenBucket,
            TokenLimit = 1,
            TokensPerPeriod = 1,
            ReplenishmentPeriod = TimeSpan.FromSeconds(10),
        };

        await _store.CheckAndIncrementAsync(
            "key1", 1, TimeSpan.FromMinutes(1), RateLimitAlgorithm.TokenBucket,
            policy, TestContext.Current.CancellationToken);

        RateLimitResult result = await _store.CheckAndIncrementAsync(
            "key2", 1, TimeSpan.FromMinutes(1), RateLimitAlgorithm.TokenBucket,
            policy, TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeTrue();
    }

    [Fact]
    public async Task TokenBucket_EmptyBucket_RetryAfterIsPositive()
    {
        RateLimitPolicyOptions policy = new()
        {
            Algorithm = RateLimitAlgorithm.TokenBucket,
            TokenLimit = 1,
            TokensPerPeriod = 1,
            ReplenishmentPeriod = TimeSpan.FromSeconds(10),
        };

        await _store.CheckAndIncrementAsync(
            "key", 1, TimeSpan.FromMinutes(1), RateLimitAlgorithm.TokenBucket,
            policy, TestContext.Current.CancellationToken);

        RateLimitResult result = await _store.CheckAndIncrementAsync(
            "key", 1, TimeSpan.FromMinutes(1), RateLimitAlgorithm.TokenBucket,
            policy, TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.RetryAfter.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task TokenBucket_PartialReplenishment_CapsAtTokenLimit()
    {
        RateLimitPolicyOptions policy = new()
        {
            Algorithm = RateLimitAlgorithm.TokenBucket,
            TokenLimit = 5,
            TokensPerPeriod = 100,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
        };

        // Consume one token
        await _store.CheckAndIncrementAsync(
            "key", 5, TimeSpan.FromMinutes(1), RateLimitAlgorithm.TokenBucket,
            policy, TestContext.Current.CancellationToken);

        // Wait for replenishment
        _timeProvider.Advance(TimeSpan.FromSeconds(5));

        RateLimitResult result = await _store.CheckAndIncrementAsync(
            "key", 5, TimeSpan.FromMinutes(1), RateLimitAlgorithm.TokenBucket,
            policy, TestContext.Current.CancellationToken);

        // Remaining should not exceed TokenLimit - 1 (one consumed)
        result.IsAllowed.ShouldBeTrue();
        result.Remaining.ShouldBeLessThanOrEqualTo(policy.TokenLimit - 1);
    }

    [Fact]
    public async Task SlidingWindow_RetryAfter_IsPositiveWhenRejected()
    {
        RateLimitPolicyOptions policy = new() { PermitLimit = 2, Window = TimeSpan.FromMinutes(1) };

        for (int i = 0; i < 2; i++)
        {
            await _store.CheckAndIncrementAsync(
                "key", 2, TimeSpan.FromMinutes(1), RateLimitAlgorithm.SlidingWindow,
                policy, TestContext.Current.CancellationToken);
        }

        RateLimitResult result = await _store.CheckAndIncrementAsync(
            "key", 2, TimeSpan.FromMinutes(1), RateLimitAlgorithm.SlidingWindow,
            policy, TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.RetryAfter.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task FixedWindow_RetryAfter_IsPositiveWhenRejected()
    {
        RateLimitPolicyOptions policy = new() { PermitLimit = 1, Window = TimeSpan.FromMinutes(1) };

        await _store.CheckAndIncrementAsync(
            "key", 1, TimeSpan.FromMinutes(1), RateLimitAlgorithm.FixedWindow,
            policy, TestContext.Current.CancellationToken);

        RateLimitResult result = await _store.CheckAndIncrementAsync(
            "key", 1, TimeSpan.FromMinutes(1), RateLimitAlgorithm.FixedWindow,
            policy, TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.RetryAfter.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ConcurrencyAlgorithm_ReturnsAllowed()
    {
        RateLimitPolicyOptions policy = new() { PermitLimit = 100 };

        RateLimitResult result = await _store.CheckAndIncrementAsync(
            "key", 100, TimeSpan.FromMinutes(1), RateLimitAlgorithm.Concurrency,
            policy, TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeTrue();
        result.Remaining.ShouldBe(100);
        result.Limit.ShouldBe(100);
    }
}
