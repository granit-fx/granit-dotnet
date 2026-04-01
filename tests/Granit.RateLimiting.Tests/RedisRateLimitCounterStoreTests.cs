using Granit.RateLimiting.Abstractions;
using Granit.RateLimiting.Internal;
using Granit.RateLimiting.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Granit.RateLimiting.Tests;

public sealed class RedisRateLimitCounterStoreTests
{
    private readonly IConnectionMultiplexer _redis = Substitute.For<IConnectionMultiplexer>();
    private readonly IDatabase _db = Substitute.For<IDatabase>();
    private readonly GranitRateLimitingOptions _rateLimitingOptions = new();
    private readonly RateLimitPolicyOptions _defaultPolicy = new()
    {
        PermitLimit = 10,
        Window = TimeSpan.FromMinutes(1),
    };

    public RedisRateLimitCounterStoreTests()
    {
        _redis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(_db);
    }

    private RedisRateLimitCounterStore CreateStore(GranitRateLimitingOptions? options = null)
    {
        GranitRateLimitingOptions effectiveOptions = options ?? _rateLimitingOptions;
        return new RedisRateLimitCounterStore(
            _redis,
            Microsoft.Extensions.Options.Options.Create(effectiveOptions),
            NullLogger<RedisRateLimitCounterStore>.Instance);
    }

    // =========================================================================
    // Sliding Window
    // =========================================================================

    [Fact]
    public async Task SlidingWindow_AllowedRequest_ReturnsAllowedResult()
    {
        RedisResult[] scriptResult = [RedisResult.Create(3), RedisResult.Create(0)];
        _db.ScriptEvaluateAsync(
                Arg.Is<string>(s => s == LuaScripts.SlidingWindow),
                Arg.Any<RedisKey[]>(),
                Arg.Any<RedisValue[]>())
            .Returns(RedisResult.Create(scriptResult));

        RedisRateLimitCounterStore store = CreateStore();

        RateLimitResult result = await store.CheckAndIncrementAsync(
            "test-key", 10, TimeSpan.FromMinutes(1),
            RateLimitAlgorithm.SlidingWindow, _defaultPolicy, TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeTrue();
        result.Remaining.ShouldBe(7);
        result.Limit.ShouldBe(10);
        result.RetryAfter.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task SlidingWindow_ExceedsLimit_ReturnsDeniedWithRetryAfter()
    {
        RedisResult[] scriptResult = [RedisResult.Create(11), RedisResult.Create(5000)];
        _db.ScriptEvaluateAsync(
                Arg.Is<string>(s => s == LuaScripts.SlidingWindow),
                Arg.Any<RedisKey[]>(),
                Arg.Any<RedisValue[]>())
            .Returns(RedisResult.Create(scriptResult));

        RedisRateLimitCounterStore store = CreateStore();

        RateLimitResult result = await store.CheckAndIncrementAsync(
            "test-key", 10, TimeSpan.FromMinutes(1),
            RateLimitAlgorithm.SlidingWindow, _defaultPolicy, TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.Remaining.ShouldBe(0);
        result.Limit.ShouldBe(10);
        result.RetryAfter.ShouldBe(TimeSpan.FromMilliseconds(5000));
    }

    [Fact]
    public async Task SlidingWindow_ExceedsLimit_ZeroOldest_UsesMinimumRetryAfter()
    {
        RedisResult[] scriptResult = [RedisResult.Create(11), RedisResult.Create(0)];
        _db.ScriptEvaluateAsync(
                Arg.Is<string>(s => s == LuaScripts.SlidingWindow),
                Arg.Any<RedisKey[]>(),
                Arg.Any<RedisValue[]>())
            .Returns(RedisResult.Create(scriptResult));

        RedisRateLimitCounterStore store = CreateStore();

        RateLimitResult result = await store.CheckAndIncrementAsync(
            "test-key", 10, TimeSpan.FromMinutes(1),
            RateLimitAlgorithm.SlidingWindow, _defaultPolicy, TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.RetryAfter.ShouldBe(TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task SlidingWindow_ExactlyAtLimit_IsAllowed()
    {
        // count == permitLimit is allowed (the check is count <= permitLimit)
        RedisResult[] scriptResult = [RedisResult.Create(10), RedisResult.Create(0)];
        _db.ScriptEvaluateAsync(
                Arg.Is<string>(s => s == LuaScripts.SlidingWindow),
                Arg.Any<RedisKey[]>(),
                Arg.Any<RedisValue[]>())
            .Returns(RedisResult.Create(scriptResult));

        RedisRateLimitCounterStore store = CreateStore();

        RateLimitResult result = await store.CheckAndIncrementAsync(
            "test-key", 10, TimeSpan.FromMinutes(1),
            RateLimitAlgorithm.SlidingWindow, _defaultPolicy, TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeTrue();
        result.Remaining.ShouldBe(0);
    }

    [Fact]
    public async Task SlidingWindow_PassesCorrectArguments()
    {
        RedisResult[] scriptResult = [RedisResult.Create(1), RedisResult.Create(0)];
        _db.ScriptEvaluateAsync(
                Arg.Any<string>(),
                Arg.Any<RedisKey[]>(),
                Arg.Any<RedisValue[]>())
            .Returns(RedisResult.Create(scriptResult));

        RedisRateLimitCounterStore store = CreateStore();
        var window = TimeSpan.FromSeconds(30);

        await store.CheckAndIncrementAsync(
            "my-key", 5, window,
            RateLimitAlgorithm.SlidingWindow, _defaultPolicy, TestContext.Current.CancellationToken);

        await _db.Received(1).ScriptEvaluateAsync(
            LuaScripts.SlidingWindow,
            Arg.Is<RedisKey[]>(keys => keys.Length == 1 && keys[0] == "my-key"),
            Arg.Is<RedisValue[]>(values => values.Length == 2
                && (long)values[0] == 30000
                && (int)values[1] == 5));
    }

    // =========================================================================
    // Fixed Window
    // =========================================================================

    [Fact]
    public async Task FixedWindow_AllowedRequest_ReturnsAllowedResult()
    {
        RedisResult[] scriptResult = [RedisResult.Create(3), RedisResult.Create(45000)];
        _db.ScriptEvaluateAsync(
                Arg.Is<string>(s => s == LuaScripts.FixedWindow),
                Arg.Any<RedisKey[]>(),
                Arg.Any<RedisValue[]>())
            .Returns(RedisResult.Create(scriptResult));

        RedisRateLimitCounterStore store = CreateStore();

        RateLimitResult result = await store.CheckAndIncrementAsync(
            "test-key", 10, TimeSpan.FromMinutes(1),
            RateLimitAlgorithm.FixedWindow, _defaultPolicy, TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeTrue();
        result.Remaining.ShouldBe(7);
        result.Limit.ShouldBe(10);
        result.RetryAfter.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task FixedWindow_ExceedsLimit_ReturnsDeniedWithTtl()
    {
        RedisResult[] scriptResult = [RedisResult.Create(11), RedisResult.Create(25000)];
        _db.ScriptEvaluateAsync(
                Arg.Is<string>(s => s == LuaScripts.FixedWindow),
                Arg.Any<RedisKey[]>(),
                Arg.Any<RedisValue[]>())
            .Returns(RedisResult.Create(scriptResult));

        RedisRateLimitCounterStore store = CreateStore();

        RateLimitResult result = await store.CheckAndIncrementAsync(
            "test-key", 10, TimeSpan.FromMinutes(1),
            RateLimitAlgorithm.FixedWindow, _defaultPolicy, TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.Remaining.ShouldBe(0);
        result.Limit.ShouldBe(10);
        result.RetryAfter.ShouldBe(TimeSpan.FromMilliseconds(25000));
    }

    [Fact]
    public async Task FixedWindow_ExceedsLimit_ZeroTtl_UsesMinimumRetryAfter()
    {
        RedisResult[] scriptResult = [RedisResult.Create(11), RedisResult.Create(0)];
        _db.ScriptEvaluateAsync(
                Arg.Is<string>(s => s == LuaScripts.FixedWindow),
                Arg.Any<RedisKey[]>(),
                Arg.Any<RedisValue[]>())
            .Returns(RedisResult.Create(scriptResult));

        RedisRateLimitCounterStore store = CreateStore();

        RateLimitResult result = await store.CheckAndIncrementAsync(
            "test-key", 10, TimeSpan.FromMinutes(1),
            RateLimitAlgorithm.FixedWindow, _defaultPolicy, TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.RetryAfter.ShouldBe(TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task FixedWindow_PassesCorrectArguments()
    {
        RedisResult[] scriptResult = [RedisResult.Create(1), RedisResult.Create(60000)];
        _db.ScriptEvaluateAsync(
                Arg.Any<string>(),
                Arg.Any<RedisKey[]>(),
                Arg.Any<RedisValue[]>())
            .Returns(RedisResult.Create(scriptResult));

        RedisRateLimitCounterStore store = CreateStore();
        var window = TimeSpan.FromSeconds(120);

        await store.CheckAndIncrementAsync(
            "fixed-key", 20, window,
            RateLimitAlgorithm.FixedWindow, _defaultPolicy, TestContext.Current.CancellationToken);

        await _db.Received(1).ScriptEvaluateAsync(
            LuaScripts.FixedWindow,
            Arg.Is<RedisKey[]>(keys => keys.Length == 1 && keys[0] == "fixed-key"),
            Arg.Is<RedisValue[]>(values => values.Length == 2
                && (long)values[0] == 120000
                && (int)values[1] == 20));
    }

    // =========================================================================
    // Token Bucket
    // =========================================================================

    [Fact]
    public async Task TokenBucket_AllowedRequest_ReturnsAllowedResult()
    {
        RedisResult[] scriptResult = [RedisResult.Create(1), RedisResult.Create(9), RedisResult.Create(0)];
        _db.ScriptEvaluateAsync(
                Arg.Is<string>(s => s == LuaScripts.TokenBucket),
                Arg.Any<RedisKey[]>(),
                Arg.Any<RedisValue[]>())
            .Returns(RedisResult.Create(scriptResult));

        RateLimitPolicyOptions policy = new()
        {
            Algorithm = RateLimitAlgorithm.TokenBucket,
            TokenLimit = 50,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(10),
        };

        RedisRateLimitCounterStore store = CreateStore();

        RateLimitResult result = await store.CheckAndIncrementAsync(
            "test-key", 50, TimeSpan.FromMinutes(1),
            RateLimitAlgorithm.TokenBucket, policy, TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeTrue();
        result.Remaining.ShouldBe(9);
        result.Limit.ShouldBe(50);
        result.RetryAfter.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task TokenBucket_EmptyBucket_ReturnsDeniedWithRetryAfter()
    {
        RedisResult[] scriptResult = [RedisResult.Create(0), RedisResult.Create(0), RedisResult.Create(8000)];
        _db.ScriptEvaluateAsync(
                Arg.Is<string>(s => s == LuaScripts.TokenBucket),
                Arg.Any<RedisKey[]>(),
                Arg.Any<RedisValue[]>())
            .Returns(RedisResult.Create(scriptResult));

        RateLimitPolicyOptions policy = new()
        {
            Algorithm = RateLimitAlgorithm.TokenBucket,
            TokenLimit = 10,
            TokensPerPeriod = 2,
            ReplenishmentPeriod = TimeSpan.FromSeconds(10),
        };

        RedisRateLimitCounterStore store = CreateStore();

        RateLimitResult result = await store.CheckAndIncrementAsync(
            "test-key", 10, TimeSpan.FromMinutes(1),
            RateLimitAlgorithm.TokenBucket, policy, TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.Remaining.ShouldBe(0);
        result.Limit.ShouldBe(10);
        result.RetryAfter.ShouldBe(TimeSpan.FromMilliseconds(8000));
    }

    [Fact]
    public async Task TokenBucket_ZeroRetryMs_UsesMinimumRetryAfter()
    {
        RedisResult[] scriptResult = [RedisResult.Create(0), RedisResult.Create(0), RedisResult.Create(0)];
        _db.ScriptEvaluateAsync(
                Arg.Is<string>(s => s == LuaScripts.TokenBucket),
                Arg.Any<RedisKey[]>(),
                Arg.Any<RedisValue[]>())
            .Returns(RedisResult.Create(scriptResult));

        RateLimitPolicyOptions policy = new()
        {
            Algorithm = RateLimitAlgorithm.TokenBucket,
            TokenLimit = 5,
            TokensPerPeriod = 1,
            ReplenishmentPeriod = TimeSpan.FromSeconds(5),
        };

        RedisRateLimitCounterStore store = CreateStore();

        RateLimitResult result = await store.CheckAndIncrementAsync(
            "test-key", 5, TimeSpan.FromMinutes(1),
            RateLimitAlgorithm.TokenBucket, policy, TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.RetryAfter.ShouldBe(TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task TokenBucket_PassesCorrectArguments()
    {
        RedisResult[] scriptResult = [RedisResult.Create(1), RedisResult.Create(4), RedisResult.Create(0)];
        _db.ScriptEvaluateAsync(
                Arg.Any<string>(),
                Arg.Any<RedisKey[]>(),
                Arg.Any<RedisValue[]>())
            .Returns(RedisResult.Create(scriptResult));

        RateLimitPolicyOptions policy = new()
        {
            Algorithm = RateLimitAlgorithm.TokenBucket,
            TokenLimit = 100,
            TokensPerPeriod = 25,
            ReplenishmentPeriod = TimeSpan.FromSeconds(30),
        };

        RedisRateLimitCounterStore store = CreateStore();

        await store.CheckAndIncrementAsync(
            "bucket-key", 100, TimeSpan.FromMinutes(1),
            RateLimitAlgorithm.TokenBucket, policy, TestContext.Current.CancellationToken);

        await _db.Received(1).ScriptEvaluateAsync(
            LuaScripts.TokenBucket,
            Arg.Is<RedisKey[]>(keys => keys.Length == 1 && keys[0] == "bucket-key"),
            Arg.Is<RedisValue[]>(values => values.Length == 3
                && (int)values[0] == 100
                && (int)values[1] == 25
                && (long)values[2] == 30000));
    }

    // =========================================================================
    // Unsupported Algorithm
    // =========================================================================

    [Fact]
    public async Task CheckAndIncrementAsync_UnsupportedAlgorithm_ThrowsNotSupportedException()
    {
        RedisRateLimitCounterStore store = CreateStore();

        await Should.ThrowAsync<NotSupportedException>(
            () => store.CheckAndIncrementAsync(
                "test-key", 10, TimeSpan.FromMinutes(1),
                (RateLimitAlgorithm)99, _defaultPolicy, TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // Redis Connection Failure — Allow fallback
    // =========================================================================

    [Fact]
    public async Task RedisConnectionException_AllowFallback_ReturnsAllowed()
    {
        _db.ScriptEvaluateAsync(
                Arg.Any<string>(),
                Arg.Any<RedisKey[]>(),
                Arg.Any<RedisValue[]>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "test failure"));

        GranitRateLimitingOptions options = new()
        {
            FallbackOnCounterStoreFailure = CounterStoreFailureBehavior.Allow,
        };

        RedisRateLimitCounterStore store = CreateStore(options);

        RateLimitResult result = await store.CheckAndIncrementAsync(
            "test-key", 10, TimeSpan.FromMinutes(1),
            RateLimitAlgorithm.SlidingWindow, _defaultPolicy, TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeTrue();
        result.Remaining.ShouldBe(10);
        result.Limit.ShouldBe(10);
        result.RetryAfter.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task RedisConnectionException_DenyFallback_ReturnsDenied()
    {
        _db.ScriptEvaluateAsync(
                Arg.Any<string>(),
                Arg.Any<RedisKey[]>(),
                Arg.Any<RedisValue[]>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "test failure"));

        GranitRateLimitingOptions options = new()
        {
            FallbackOnCounterStoreFailure = CounterStoreFailureBehavior.Deny,
        };

        RedisRateLimitCounterStore store = CreateStore(options);

        RateLimitResult result = await store.CheckAndIncrementAsync(
            "test-key", 10, TimeSpan.FromMinutes(1),
            RateLimitAlgorithm.SlidingWindow, _defaultPolicy, TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.Remaining.ShouldBe(0);
        result.Limit.ShouldBe(10);
        result.RetryAfter.ShouldBe(TimeSpan.FromSeconds(1));
    }

    // =========================================================================
    // Redis Timeout Failure
    // =========================================================================

    [Fact]
    public async Task RedisTimeoutException_AllowFallback_ReturnsAllowed()
    {
        _db.ScriptEvaluateAsync(
                Arg.Any<string>(),
                Arg.Any<RedisKey[]>(),
                Arg.Any<RedisValue[]>())
            .ThrowsAsync(new RedisTimeoutException("timeout", CommandStatus.WaitingToBeSent));

        GranitRateLimitingOptions options = new()
        {
            FallbackOnCounterStoreFailure = CounterStoreFailureBehavior.Allow,
        };

        RedisRateLimitCounterStore store = CreateStore(options);

        RateLimitResult result = await store.CheckAndIncrementAsync(
            "test-key", 10, TimeSpan.FromMinutes(1),
            RateLimitAlgorithm.FixedWindow, _defaultPolicy, TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeTrue();
        result.Remaining.ShouldBe(10);
        result.Limit.ShouldBe(10);
    }

    [Fact]
    public async Task RedisTimeoutException_DenyFallback_ReturnsDenied()
    {
        _db.ScriptEvaluateAsync(
                Arg.Any<string>(),
                Arg.Any<RedisKey[]>(),
                Arg.Any<RedisValue[]>())
            .ThrowsAsync(new RedisTimeoutException("timeout", CommandStatus.WaitingToBeSent));

        GranitRateLimitingOptions options = new()
        {
            FallbackOnCounterStoreFailure = CounterStoreFailureBehavior.Deny,
        };

        RedisRateLimitCounterStore store = CreateStore(options);

        RateLimitResult result = await store.CheckAndIncrementAsync(
            "test-key", 10, TimeSpan.FromMinutes(1),
            RateLimitAlgorithm.TokenBucket, _defaultPolicy, TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.Remaining.ShouldBe(0);
        result.RetryAfter.ShouldBe(TimeSpan.FromSeconds(1));
    }

    // =========================================================================
    // Algorithm routing
    // =========================================================================

    [Theory]
    [InlineData(RateLimitAlgorithm.SlidingWindow)]
    [InlineData(RateLimitAlgorithm.FixedWindow)]
    [InlineData(RateLimitAlgorithm.TokenBucket)]
    public async Task CheckAndIncrementAsync_AllSupportedAlgorithms_CallsScriptEvaluate(RateLimitAlgorithm algorithm)
    {
        // Set up a generic response that works for all three algorithms
        RedisResult[] scriptResult = algorithm == RateLimitAlgorithm.TokenBucket
            ? [RedisResult.Create(1), RedisResult.Create(5), RedisResult.Create(0)]
            : [RedisResult.Create(1), RedisResult.Create(0)];

        _db.ScriptEvaluateAsync(
                Arg.Any<string>(),
                Arg.Any<RedisKey[]>(),
                Arg.Any<RedisValue[]>())
            .Returns(RedisResult.Create(scriptResult));

        RateLimitPolicyOptions policy = new()
        {
            TokenLimit = 10,
            TokensPerPeriod = 2,
            ReplenishmentPeriod = TimeSpan.FromSeconds(10),
        };

        RedisRateLimitCounterStore store = CreateStore();

        RateLimitResult result = await store.CheckAndIncrementAsync(
            "test-key", 10, TimeSpan.FromMinutes(1),
            algorithm, policy, TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeTrue();
        await _db.Received(1).ScriptEvaluateAsync(
            Arg.Any<string>(),
            Arg.Any<RedisKey[]>(),
            Arg.Any<RedisValue[]>());
    }

    // =========================================================================
    // Failure with different algorithms
    // =========================================================================

    [Theory]
    [InlineData(RateLimitAlgorithm.SlidingWindow)]
    [InlineData(RateLimitAlgorithm.FixedWindow)]
    [InlineData(RateLimitAlgorithm.TokenBucket)]
    public async Task RedisConnectionException_HandledForAllAlgorithms(RateLimitAlgorithm algorithm)
    {
        _db.ScriptEvaluateAsync(
                Arg.Any<string>(),
                Arg.Any<RedisKey[]>(),
                Arg.Any<RedisValue[]>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        GranitRateLimitingOptions options = new()
        {
            FallbackOnCounterStoreFailure = CounterStoreFailureBehavior.Allow,
        };

        RateLimitPolicyOptions policy = new()
        {
            TokenLimit = 10,
            TokensPerPeriod = 2,
            ReplenishmentPeriod = TimeSpan.FromSeconds(10),
        };

        RedisRateLimitCounterStore store = CreateStore(options);

        RateLimitResult result = await store.CheckAndIncrementAsync(
            "test-key", 10, TimeSpan.FromMinutes(1),
            algorithm, policy, TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeTrue();
    }
}
