// =============================================================================
// Integration tests — FusionCache fail-safe and factory timeouts
// =============================================================================
// Validates fail-safe behavior (stale data on factory failure) and factory
// soft/hard timeouts with a real Redis L2 backend.
//
// Requires Docker (Testcontainers spins up a Redis container).
// =============================================================================

using Microsoft.Extensions.Caching.StackExchangeRedis;
using Shouldly;
using StackExchange.Redis;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Caching.Tests.Integration;

public sealed class FailSafeIntegrationTests(
    RedisContainerFixture redis) : IClassFixture<RedisContainerFixture>, IAsyncDisposable
{
    private IFusionCache? _cache;

    public async ValueTask DisposeAsync()
    {
        if (_cache is IAsyncDisposable d)
        {
            await d.DisposeAsync();
        }
    }

    [Fact]
    public async Task FailSafe_WhenFactoryThrows_ReturnsStaleValue()
    {
        // Arrange
        _cache = await CreateCacheAsync(failSafe: true, duration: TimeSpan.FromMilliseconds(100));

        // Populate cache with a value
        await _cache.SetAsync("fs-key", "original-value",
            new FusionCacheEntryOptions
            {
                Duration = TimeSpan.FromMilliseconds(100),
                IsFailSafeEnabled = true,
                FailSafeMaxDuration = TimeSpan.FromHours(1),
            },
            token: TestContext.Current.CancellationToken);

        // Wait for entry to expire
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Act — factory throws, fail-safe should return stale value
        string? result = await _cache.GetOrSetAsync<string>("fs-key",
            (_, _) => throw new InvalidOperationException("DB is down"),
            new FusionCacheEntryOptions
            {
                Duration = TimeSpan.FromMinutes(5),
                IsFailSafeEnabled = true,
                FailSafeMaxDuration = TimeSpan.FromHours(1),
            },
            token: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe("original-value");
    }

    [Fact]
    public async Task FailSafe_WhenNoStaleValueAndFactoryThrows_ReturnsDefault()
    {
        // Arrange
        _cache = await CreateCacheAsync(failSafe: true, duration: TimeSpan.FromMinutes(5));

        // Act — no stale value exists, factory throws
        string? result = await _cache.GetOrDefaultAsync<string>("no-stale-key",
            token: TestContext.Current.CancellationToken);

        // Assert — no stale, no factory, returns default
        result.ShouldBeNull();
    }

    [Fact]
    public async Task FactorySoftTimeout_WhenFactoryIsSlow_ReturnsStaleWithinTimeout()
    {
        // Arrange
        _cache = await CreateCacheAsync(failSafe: true, duration: TimeSpan.FromMilliseconds(100));

        // Populate cache
        await _cache.SetAsync("timeout-key", "stale-value",
            new FusionCacheEntryOptions
            {
                Duration = TimeSpan.FromMilliseconds(100),
                IsFailSafeEnabled = true,
                FailSafeMaxDuration = TimeSpan.FromHours(1),
            },
            token: TestContext.Current.CancellationToken);

        // Wait for expiration
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Act — factory is slow (3s), soft timeout is 500ms
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string? result = await _cache.GetOrSetAsync<string>("timeout-key",
            async (_, ct) =>
            {
                await Task.Delay(3000, ct);
                return "fresh-value";
            },
            new FusionCacheEntryOptions
            {
                Duration = TimeSpan.FromMinutes(5),
                IsFailSafeEnabled = true,
                FailSafeMaxDuration = TimeSpan.FromHours(1),
                FactorySoftTimeout = TimeSpan.FromMilliseconds(500),
                FactoryHardTimeout = TimeSpan.FromSeconds(10),
                AllowBackgroundDistributedCacheOperations = true,
            },
            token: TestContext.Current.CancellationToken);
        sw.Stop();

        // Assert — stale value returned within soft timeout (not 3s)
        result.ShouldBe("stale-value");
        sw.ElapsedMilliseconds.ShouldBeLessThan(2000,
            "Should return stale value within soft timeout, not wait for slow factory");
    }

    [Fact]
    public async Task FailSafe_Disabled_WhenFactoryThrows_PropagatesException()
    {
        // Arrange
        _cache = await CreateCacheAsync(failSafe: false, duration: TimeSpan.FromMilliseconds(100));

        await _cache.SetAsync("no-fs-key", "value",
            new FusionCacheEntryOptions
            {
                Duration = TimeSpan.FromMilliseconds(100),
                IsFailSafeEnabled = false,
            },
            token: TestContext.Current.CancellationToken);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Act & Assert — without fail-safe, exception propagates
        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await _cache.GetOrSetAsync<string>("no-fs-key",
                (_, _) => throw new InvalidOperationException("DB is down"),
                new FusionCacheEntryOptions
                {
                    Duration = TimeSpan.FromMinutes(5),
                    IsFailSafeEnabled = false,
                },
                token: TestContext.Current.CancellationToken);
        });
    }

    private async Task<IFusionCache> CreateCacheAsync(bool failSafe, TimeSpan duration)
    {
        IConnectionMultiplexer mux = await ConnectionMultiplexer.ConnectAsync(
            redis.ConnectionString);

        var redisCache = new RedisCache(MsOptions.Create(new RedisCacheOptions
        {
            ConnectionMultiplexerFactory = () => Task.FromResult(mux),
            InstanceName = "fs-test:",
        }));

        var cache = new FusionCache(
            new FusionCacheOptions
            {
                CacheName = $"failsafe-{Guid.NewGuid():N}",
                DefaultEntryOptions = new FusionCacheEntryOptions
                {
                    Duration = duration,
                    IsFailSafeEnabled = failSafe,
                    FailSafeMaxDuration = TimeSpan.FromHours(1),
                    FailSafeThrottleDuration = TimeSpan.FromSeconds(1),
                },
            });

        cache.SetupSerializer(new FusionCacheSystemTextJsonSerializer());
        cache.SetupDistributedCache(redisCache);

        return cache;
    }
}
