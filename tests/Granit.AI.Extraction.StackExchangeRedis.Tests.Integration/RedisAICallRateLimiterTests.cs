using Granit.AI.Extraction.RateLimiting;
using Granit.AI.Extraction.StackExchangeRedis.Extensions;
using Granit.AI.Extraction.StackExchangeRedis.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Granit.AI.Extraction.StackExchangeRedis.Tests.Integration;

public sealed class RedisAICallRateLimiterTests(RedisContainerFixture fixture) : IClassFixture<RedisContainerFixture>
{
    [Fact]
    public async Task Admits_up_to_the_cap_then_denies()
    {
        await using ServiceProvider sp = BuildProvider(fixture.ConnectionString);
        IAICallRateLimiter limiter = sp.GetRequiredService<IAICallRateLimiter>();
        string bucket = NewBucket();

        bool first = await limiter.TryAcquireAsync(bucket, 3, TestContext.Current.CancellationToken);
        bool second = await limiter.TryAcquireAsync(bucket, 3, TestContext.Current.CancellationToken);
        bool third = await limiter.TryAcquireAsync(bucket, 3, TestContext.Current.CancellationToken);
        bool fourth = await limiter.TryAcquireAsync(bucket, 3, TestContext.Current.CancellationToken);

        first.ShouldBeTrue();
        second.ShouldBeTrue();
        third.ShouldBeTrue();
        fourth.ShouldBeFalse();
    }

    [Fact]
    public async Task Two_replicas_sharing_one_redis_converge_to_a_single_shared_ceiling()
    {
        // The whole point of the package: with the in-memory limiter each replica would
        // admit `cap` calls (cap × N total). Backed by Redis, two independent limiter
        // instances (two connections, two service providers = two pods) MUST share one
        // ceiling.
        await using ServiceProvider podA = BuildProvider(fixture.ConnectionString);
        await using ServiceProvider podB = BuildProvider(fixture.ConnectionString);
        IAICallRateLimiter limiterA = podA.GetRequiredService<IAICallRateLimiter>();
        IAICallRateLimiter limiterB = podB.GetRequiredService<IAICallRateLimiter>();
        string bucket = NewBucket();
        const int cap = 4;

        int admitted = 0;
        for (int i = 0; i < 10; i++)
        {
            IAICallRateLimiter limiter = i % 2 == 0 ? limiterA : limiterB;
            if (await limiter.TryAcquireAsync(bucket, cap, TestContext.Current.CancellationToken))
            {
                admitted++;
            }
        }

        admitted.ShouldBe(cap);
    }

    [Fact]
    public async Task Distinct_buckets_have_independent_ceilings()
    {
        await using ServiceProvider sp = BuildProvider(fixture.ConnectionString);
        IAICallRateLimiter limiter = sp.GetRequiredService<IAICallRateLimiter>();
        string tenantA = NewBucket();
        string tenantB = NewBucket();

        (await limiter.TryAcquireAsync(tenantA, 1, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await limiter.TryAcquireAsync(tenantA, 1, TestContext.Current.CancellationToken)).ShouldBeFalse();

        // tenantB is untouched by tenantA hitting its ceiling.
        (await limiter.TryAcquireAsync(tenantB, 1, TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task Key_prefix_isolates_apps_sharing_one_redis()
    {
        // Same bucket key, different app prefixes → independent counters, so one Granit
        // app cannot consume another's ceiling on a shared Redis.
        await using ServiceProvider appA = BuildProvider(fixture.ConnectionString, o => o.KeyPrefix = "granit:ai:ratelimit:appA:");
        await using ServiceProvider appB = BuildProvider(fixture.ConnectionString, o => o.KeyPrefix = "granit:ai:ratelimit:appB:");
        IAICallRateLimiter limiterA = appA.GetRequiredService<IAICallRateLimiter>();
        IAICallRateLimiter limiterB = appB.GetRequiredService<IAICallRateLimiter>();
        string bucket = NewBucket();

        (await limiterA.TryAcquireAsync(bucket, 1, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await limiterA.TryAcquireAsync(bucket, 1, TestContext.Current.CancellationToken)).ShouldBeFalse();

        // appB has its own counter despite the identical bucket key.
        (await limiterB.TryAcquireAsync(bucket, 1, TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    private static ServiceProvider BuildProvider(
        string connectionString,
        Action<AIRateLimitingRedisOptions>? configure = null)
    {
        ServiceCollection services = [];
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(connectionString));
        services.AddGranitAIExtractionRedisRateLimiter();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services.BuildServiceProvider();
    }

    private static string NewBucket() => $"test:{Guid.NewGuid():N}";
}
