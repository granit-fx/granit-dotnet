using System.Diagnostics.Metrics;
using Granit.Features.Diagnostics;
using Granit.Features.Exceptions;
using Granit.Features.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Features.Tests.Limits;

public sealed class FeatureLimitGuardTests
{
    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];
        public Meter Create(MeterOptions options) { Meter m = new(options); _meters.Add(m); return m; }
        public void Dispose() { foreach (Meter m in _meters) { m.Dispose(); } }
    }

    private static FeatureLimitGuard BuildGuard(long resolvedLimit)
    {
        IFeatureChecker checker = Substitute.For<IFeatureChecker>();
        checker.GetNumericAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(resolvedLimit);
        ServiceCollection sc = new();
        ServiceProvider sp = sc.BuildServiceProvider();
        FeaturesMetrics metrics = new(new TestMeterFactory());
        return new FeatureLimitGuard(checker, sp, metrics, NullLogger<FeatureLimitGuard>.Instance);
    }

    [Fact]
    public async Task CheckAsync_BelowLimit_DoesNotThrow()
    {
        FeatureLimitGuard guard = BuildGuard(resolvedLimit: 100);

        Func<Task> act = () => guard.CheckAsync("App.MaxPatients", currentCount: 50,
            TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task CheckAsync_AtLimit_Throws_FeatureLimitExceededException()
    {
        FeatureLimitGuard guard = BuildGuard(resolvedLimit: 50);

        Func<Task> act = () => guard.CheckAsync("App.MaxPatients", currentCount: 50,
            TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<FeatureLimitExceededException>(act)).Message.ShouldContain("App.MaxPatients");
    }

    [Fact]
    public async Task CheckAsync_AboveLimit_Throws_FeatureLimitExceededException()
    {
        FeatureLimitGuard guard = BuildGuard(resolvedLimit: 50);

        Func<Task> act = () => guard.CheckAsync("App.MaxPatients", currentCount: 99,
            TestContext.Current.CancellationToken);

        await Should.ThrowAsync<FeatureLimitExceededException>(act);
    }

    [Fact]
    public async Task GetLimitAsync_Returns_ResolvedNumericValue()
    {
        FeatureLimitGuard guard = BuildGuard(resolvedLimit: 200);

        long limit = await guard.GetLimitAsync("App.MaxPatients", TestContext.Current.CancellationToken);

        limit.ShouldBe(200);
    }

    [Fact]
    public async Task FeatureLimitExceededException_Carries_Context()
    {
        FeatureLimitGuard guard = BuildGuard(resolvedLimit: 10);

        FeatureLimitExceededException? ex = null;
        try
        {
            await guard.CheckAsync("App.MaxPatients", currentCount: 10,
                TestContext.Current.CancellationToken);
        }
        catch (FeatureLimitExceededException caught)
        {
            ex = caught;
        }

        ex.ShouldNotBeNull();
        ex!.FeatureName.ShouldBe("App.MaxPatients");
        ex.Current.ShouldBe(10);
        ex.Limit.ShouldBe(10);
        ex.ErrorCode.ShouldBe("Features:LimitExceeded");
    }
}
