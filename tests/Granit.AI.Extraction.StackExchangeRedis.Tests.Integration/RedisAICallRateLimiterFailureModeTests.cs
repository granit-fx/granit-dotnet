using Granit.AI.Extraction.RateLimiting;
using Granit.AI.Extraction.StackExchangeRedis.Extensions;
using Granit.AI.Extraction.StackExchangeRedis.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Granit.AI.Extraction.StackExchangeRedis.Tests.Integration;

/// <summary>
/// Redis-failure fallback behaviour. No container — the connection multiplexer is
/// substituted so its script evaluation throws a transport error.
/// </summary>
public sealed class RedisAICallRateLimiterFailureModeTests
{
    [Fact]
    public async Task Denies_by_default_when_redis_is_unavailable()
    {
        // Fail closed: the consumer falls through to its non-AI path, which protects the
        // cost ceiling during a Redis outage rather than letting the gate fail open.
        await using ServiceProvider sp = BuildProvider(allowOnFailure: false);
        IAICallRateLimiter limiter = sp.GetRequiredService<IAICallRateLimiter>();

        bool admitted = await limiter.TryAcquireAsync("bucket", 1000, TestContext.Current.CancellationToken);

        admitted.ShouldBeFalse();
    }

    [Fact]
    public async Task Allows_when_AllowOnRedisFailure_is_set()
    {
        await using ServiceProvider sp = BuildProvider(allowOnFailure: true);
        IAICallRateLimiter limiter = sp.GetRequiredService<IAICallRateLimiter>();

        bool admitted = await limiter.TryAcquireAsync("bucket", 1000, TestContext.Current.CancellationToken);

        admitted.ShouldBeTrue();
    }

    private static ServiceProvider BuildProvider(bool allowOnFailure)
    {
        IDatabase db = Substitute.For<IDatabase>();
        db.ScriptEvaluateAsync(
                Arg.Any<string>(),
                Arg.Any<RedisKey[]>(),
                Arg.Any<RedisValue[]>(),
                Arg.Any<CommandFlags>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "simulated outage"));

        IConnectionMultiplexer mux = Substitute.For<IConnectionMultiplexer>();
        mux.GetDatabase().Returns(db);

        ServiceCollection services = [];
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddSingleton(mux);
        services.AddGranitAIExtractionRedisRateLimiter();
        services.Configure<AIRateLimitingRedisOptions>(o => o.AllowOnRedisFailure = allowOnFailure);

        return services.BuildServiceProvider();
    }
}
