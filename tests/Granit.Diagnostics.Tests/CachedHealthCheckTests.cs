using Granit.Diagnostics.Caching;
using Granit.Timing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Diagnostics.Tests;

public sealed class CachedHealthCheckTests
{
    private readonly IClock _clock;

    public CachedHealthCheckTests()
    {
        _clock = Substitute.For<IClock>();
        _clock.Now.Returns(_ => DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsInnerResult_WhenCacheIsEmpty()
    {
        IHealthCheck inner = Substitute.For<IHealthCheck>();
        inner.CheckHealthAsync(Arg.Any<HealthCheckContext>(), Arg.Any<CancellationToken>())
            .Returns(HealthCheckResult.Healthy());

        CachedHealthCheck sut = new(inner, TimeSpan.FromSeconds(10), _clock);
        HealthCheckContext context = BuildContext();

        HealthCheckResult result = await sut.CheckHealthAsync(context, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
        await inner.Received(1).CheckHealthAsync(Arg.Any<HealthCheckContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsCachedResult_WithoutCallingInner()
    {
        IHealthCheck inner = Substitute.For<IHealthCheck>();
        inner.CheckHealthAsync(Arg.Any<HealthCheckContext>(), Arg.Any<CancellationToken>())
            .Returns(HealthCheckResult.Healthy());

        CachedHealthCheck sut = new(inner, TimeSpan.FromSeconds(30), _clock);
        HealthCheckContext context = BuildContext();

        await sut.CheckHealthAsync(context, TestContext.Current.CancellationToken);
        HealthCheckResult cached = await sut.CheckHealthAsync(context, TestContext.Current.CancellationToken);

        cached.Status.ShouldBe(HealthStatus.Healthy);
        await inner.Received(1).CheckHealthAsync(Arg.Any<HealthCheckContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckHealthAsync_ExecutesInnerOnce_WhenCalledConcurrently()
    {
        int callCount = 0;
        IHealthCheck inner = Substitute.For<IHealthCheck>();
        inner.CheckHealthAsync(Arg.Any<HealthCheckContext>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                Interlocked.Increment(ref callCount);
                await Task.Delay(50); // simulate slow dependency
                return HealthCheckResult.Healthy();
            });

        CachedHealthCheck sut = new(inner, TimeSpan.FromSeconds(30), _clock);
        HealthCheckContext context = BuildContext();

        // 10 concurrent callers — only 1 should hit the inner check
        IEnumerable<Task<HealthCheckResult>> tasks = Enumerable.Range(0, 10)
            .Select(_ => sut.CheckHealthAsync(context, TestContext.Current.CancellationToken));

        HealthCheckResult[] results = await Task.WhenAll(tasks);

        callCount.ShouldBe(1);
        results.ToList().ForEach(r => r.Status.ShouldBe(HealthStatus.Healthy));
    }

    [Fact]
    public async Task CheckHealthAsync_ReExecutesInner_AfterCacheExpires()
    {
        IHealthCheck inner = Substitute.For<IHealthCheck>();
        inner.CheckHealthAsync(Arg.Any<HealthCheckContext>(), Arg.Any<CancellationToken>())
            .Returns(HealthCheckResult.Healthy());

        // Very short TTL so it expires immediately
        CachedHealthCheck sut = new(inner, TimeSpan.FromMilliseconds(1), _clock);
        HealthCheckContext context = BuildContext();

        await sut.CheckHealthAsync(context, TestContext.Current.CancellationToken);
        await Task.Delay(10, TestContext.Current.CancellationToken); // wait for TTL to expire
        await sut.CheckHealthAsync(context, TestContext.Current.CancellationToken);

        await inner.Received(2).CheckHealthAsync(Arg.Any<HealthCheckContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckHealthAsync_CachesDegradedResult()
    {
        IHealthCheck inner = Substitute.For<IHealthCheck>();
        inner.CheckHealthAsync(Arg.Any<HealthCheckContext>(), Arg.Any<CancellationToken>())
            .Returns(HealthCheckResult.Degraded("slow dependency"));

        CachedHealthCheck sut = new(inner, TimeSpan.FromSeconds(30), _clock);
        HealthCheckContext context = BuildContext();

        await sut.CheckHealthAsync(context, TestContext.Current.CancellationToken);
        HealthCheckResult result = await sut.CheckHealthAsync(context, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Degraded);
        await inner.Received(1).CheckHealthAsync(Arg.Any<HealthCheckContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Dispose_ReleasesLock_WithoutThrowing()
    {
        // Arrange
        IHealthCheck inner = Substitute.For<IHealthCheck>();
        CachedHealthCheck sut = new(inner, TimeSpan.FromSeconds(10), _clock);

        // Act & Assert — Dispose must not throw; SemaphoreSlim is released
        Action act = sut.Dispose;
        Should.NotThrow(act);
    }

    [Fact]
    public async Task CheckHealthAsync_SecondConcurrentCaller_UsesDoubleCheckLockAndReturnsCachedResult()
    {
        // This test explicitly covers the double-check path (line 50):
        // Two callers enter before the cache is warm. The first acquires the lock,
        // populates the cache, then releases it. The second acquires the lock, hits
        // the double-check (cache is now warm), and returns the cached result without
        // calling inner again.
        SemaphoreSlim firstCallerStarted = new(0, 1);
        SemaphoreSlim firstCallerCanContinue = new(0, 1);
        int callCount = 0;

        IHealthCheck inner = Substitute.For<IHealthCheck>();
        inner.CheckHealthAsync(Arg.Any<HealthCheckContext>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                Interlocked.Increment(ref callCount);
                firstCallerStarted.Release();
                await firstCallerCanContinue.WaitAsync();
                return HealthCheckResult.Healthy("populated");
            });

        CachedHealthCheck sut = new(inner, TimeSpan.FromSeconds(30), _clock);
        HealthCheckContext context = BuildContext();

        // First caller takes the lock and waits
        Task<HealthCheckResult> firstCall = sut.CheckHealthAsync(context, TestContext.Current.CancellationToken);

        // Wait until inner is executing (lock is held by first caller)
        await firstCallerStarted.WaitAsync(TestContext.Current.CancellationToken);

        // Second caller tries to enter — it will block on WaitAsync
        Task<HealthCheckResult> secondCall = sut.CheckHealthAsync(context, TestContext.Current.CancellationToken);

        // Let first caller finish, which populates the cache and releases the lock
        firstCallerCanContinue.Release();
        HealthCheckResult firstResult = await firstCall;

        // Second caller gets the lock, hits the double-check, and returns cached result
        HealthCheckResult secondResult = await secondCall;

        // Only one call to inner
        callCount.ShouldBe(1);
        firstResult.Status.ShouldBe(HealthStatus.Healthy);
        secondResult.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_CachesUnhealthyResult()
    {
        IHealthCheck inner = Substitute.For<IHealthCheck>();
        inner.CheckHealthAsync(Arg.Any<HealthCheckContext>(), Arg.Any<CancellationToken>())
            .Returns(HealthCheckResult.Unhealthy("connection refused"));

        CachedHealthCheck sut = new(inner, TimeSpan.FromSeconds(30), _clock);
        HealthCheckContext context = BuildContext();

        await sut.CheckHealthAsync(context, TestContext.Current.CancellationToken);
        HealthCheckResult result = await sut.CheckHealthAsync(context, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldBe("connection refused");
        await inner.Received(1).CheckHealthAsync(Arg.Any<HealthCheckContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckHealthAsync_UsesClockForExpiry_NotSystemTime()
    {
        DateTimeOffset fixedTime = new(2026, 3, 20, 12, 0, 0, TimeSpan.Zero);
        IClock fakeClock = Substitute.For<IClock>();
        fakeClock.Now.Returns(_ => fixedTime);

        IHealthCheck inner = Substitute.For<IHealthCheck>();
        inner.CheckHealthAsync(Arg.Any<HealthCheckContext>(), Arg.Any<CancellationToken>())
            .Returns(HealthCheckResult.Healthy());

        CachedHealthCheck sut = new(inner, TimeSpan.FromSeconds(10), fakeClock);
        HealthCheckContext context = BuildContext();

        // First call populates cache
        await sut.CheckHealthAsync(context, TestContext.Current.CancellationToken);

        // Still within cache window
        fakeClock.Now.Returns(_ => fixedTime.AddSeconds(9));
        await sut.CheckHealthAsync(context, TestContext.Current.CancellationToken);

        await inner.Received(1).CheckHealthAsync(Arg.Any<HealthCheckContext>(), Arg.Any<CancellationToken>());

        // After cache expiry
        fakeClock.Now.Returns(_ => fixedTime.AddSeconds(11));
        await sut.CheckHealthAsync(context, TestContext.Current.CancellationToken);

        await inner.Received(2).CheckHealthAsync(Arg.Any<HealthCheckContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckHealthAsync_PreservesDescription_InCachedResult()
    {
        IHealthCheck inner = Substitute.For<IHealthCheck>();
        inner.CheckHealthAsync(Arg.Any<HealthCheckContext>(), Arg.Any<CancellationToken>())
            .Returns(HealthCheckResult.Degraded("pool exhausted"));

        CachedHealthCheck sut = new(inner, TimeSpan.FromSeconds(30), _clock);
        HealthCheckContext context = BuildContext();

        await sut.CheckHealthAsync(context, TestContext.Current.CancellationToken);
        HealthCheckResult cached = await sut.CheckHealthAsync(context, TestContext.Current.CancellationToken);

        cached.Description.ShouldBe("pool exhausted");
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes_WithoutThrowing()
    {
        IHealthCheck inner = Substitute.For<IHealthCheck>();
        CachedHealthCheck sut = new(inner, TimeSpan.FromSeconds(10), _clock);

        // First dispose should not throw
        Should.NotThrow(sut.Dispose);

        // Second dispose on an already disposed SemaphoreSlim should not throw either
        // (SemaphoreSlim.Dispose is idempotent)
        Should.NotThrow(sut.Dispose);
    }

    private static HealthCheckContext BuildContext() =>
        new() { Registration = new HealthCheckRegistration("test", _ => Substitute.For<IHealthCheck>(), null, []) };
}
