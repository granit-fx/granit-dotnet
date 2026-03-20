// =============================================================================
// Integration tests — FusionCache Redis backplane
// =============================================================================
// Validates cross-instance L1 cache invalidation via Redis pub/sub backplane.
// Two independent FusionCache instances share the same Redis L2 + backplane.
//
// Requires Docker (Testcontainers spins up a Redis container).
// The container is shared across the class via IClassFixture.
// =============================================================================

using Microsoft.Extensions.Caching.StackExchangeRedis;
using Shouldly;
using StackExchange.Redis;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Caching.Tests.Integration;

public sealed class BackplaneIntegrationTests(
    RedisContainerFixture redis) : IClassFixture<RedisContainerFixture>, IAsyncDisposable
{
    private readonly List<IFusionCache> _caches = [];

    public async ValueTask DisposeAsync()
    {
        foreach (IFusionCache cache in _caches)
        {
            if (cache is IAsyncDisposable d)
            {
                await d.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task SetOnInstanceA_IsReadableOnInstanceB_ViaL2()
    {
        // Arrange
        (IFusionCache cacheA, IFusionCache cacheB) = await CreatePairAsync(TestContext.Current.CancellationToken);

        // Act — write on A
        await cacheA.SetAsync("shared-key", "hello-from-A",
            token: TestContext.Current.CancellationToken);

        // Assert — B reads from L2 (Redis) on cache miss
        string? value = await cacheB.GetOrDefaultAsync<string>("shared-key",
            token: TestContext.Current.CancellationToken);

        value.ShouldBe("hello-from-A");
    }

    [Fact]
    public async Task ExpireOnInstanceA_InvalidatesL1OnInstanceB_ViaBackplane()
    {
        // Arrange
        (IFusionCache cacheA, IFusionCache cacheB) = await CreatePairAsync(TestContext.Current.CancellationToken);

        // Populate on both instances (both have L1 populated)
        await cacheA.SetAsync("bp-key", "original",
            token: TestContext.Current.CancellationToken);

        // Force B to read and populate its L1
        string? warmup = await cacheB.GetOrDefaultAsync<string>("bp-key",
            token: TestContext.Current.CancellationToken);
        warmup.ShouldBe("original");

        // Act — expire on A (backplane notifies B)
        await cacheA.ExpireAsync("bp-key",
            token: TestContext.Current.CancellationToken);

        // Wait for backplane notification propagation
        await Task.Delay(500, TestContext.Current.CancellationToken);

        // Assert — B's next read should trigger factory (L1 invalidated by backplane)
        bool factoryCalled = false;
        string? result = await cacheB.GetOrSetAsync<string>("bp-key",
            async (_, ct) =>
            {
                factoryCalled = true;
                return "refreshed";
            },
            token: TestContext.Current.CancellationToken);

        factoryCalled.ShouldBeTrue("Backplane should have invalidated B's L1, triggering the factory");
        result.ShouldBe("refreshed");
    }

    [Fact]
    public async Task RemoveOnInstanceA_DeletesFromL2_InstanceBGetsMiss()
    {
        // Arrange
        (IFusionCache cacheA, IFusionCache cacheB) = await CreatePairAsync(TestContext.Current.CancellationToken);

        await cacheA.SetAsync("rm-key", "to-delete",
            token: TestContext.Current.CancellationToken);

        // Verify B can read it
        string? before = await cacheB.GetOrDefaultAsync<string>("rm-key",
            token: TestContext.Current.CancellationToken);
        before.ShouldBe("to-delete");

        // Act — hard remove on A
        await cacheA.RemoveAsync("rm-key",
            token: TestContext.Current.CancellationToken);

        // Wait for backplane
        await Task.Delay(500, TestContext.Current.CancellationToken);

        // Assert — B gets null (entry gone from L2)
        string? after = await cacheB.GetOrDefaultAsync<string>("rm-key",
            token: TestContext.Current.CancellationToken);
        after.ShouldBeNull();
    }

    /// <summary>
    /// Creates two independent FusionCache instances sharing the same Redis L2 + backplane.
    /// Each has its own L1 memory cache (simulating two pods).
    /// </summary>
    private async Task<(IFusionCache A, IFusionCache B)> CreatePairAsync(
        CancellationToken cancellationToken)
    {
        IFusionCache a = await CreateInstanceAsync("instance-a", cancellationToken);
        IFusionCache b = await CreateInstanceAsync("instance-b", cancellationToken);
        _caches.Add(a);
        _caches.Add(b);
        return (a, b);
    }

    private async Task<IFusionCache> CreateInstanceAsync(string instanceName,
        CancellationToken cancellationToken)
    {
        IConnectionMultiplexer mux = await ConnectionMultiplexer.ConnectAsync(
            redis.ConnectionString);

        var redisCache = new RedisCache(MsOptions.Create(new RedisCacheOptions
        {
            ConnectionMultiplexerFactory = () => Task.FromResult(mux),
            InstanceName = "test:",
        }));

        var backplane = new RedisBackplane(
            MsOptions.Create(new RedisBackplaneOptions
            {
                ConnectionMultiplexerFactory = () => Task.FromResult(mux),
            }));

        var cache = new FusionCache(
            new FusionCacheOptions
            {
                CacheName = instanceName,
                DefaultEntryOptions = new FusionCacheEntryOptions
                {
                    Duration = TimeSpan.FromMinutes(5),
                    IsFailSafeEnabled = true,
                    FailSafeMaxDuration = TimeSpan.FromHours(1),
                },
                EnableAutoRecovery = true,
                BackplaneChannelPrefix = "test:bp",
            });

        cache.SetupSerializer(new FusionCacheSystemTextJsonSerializer());
        cache.SetupDistributedCache(redisCache);
        cache.SetupBackplane(backplane);

        // Allow backplane subscription to settle
        await Task.Delay(200, cancellationToken);

        return cache;
    }
}
