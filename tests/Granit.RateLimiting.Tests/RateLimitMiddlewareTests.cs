using System.Diagnostics.Metrics;
using Granit.MultiTenancy;
using Granit.RateLimiting.Abstractions;
using Granit.RateLimiting.Attributes;
using Granit.RateLimiting.Diagnostics;
using Granit.RateLimiting.Exceptions;
using Granit.RateLimiting.Internal;
using Granit.RateLimiting.Options;
using Granit.RateLimiting.Wolverine;
using Granit.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.RateLimiting.Tests;

public sealed class RateLimitMiddlewareTests : IDisposable
{
    [RateLimited("api")]
    private sealed class RateLimitedMessage;

    private sealed class PlainMessage;

    private readonly IRateLimitCounterStore _counterStore = Substitute.For<IRateLimitCounterStore>();
    private readonly IRateLimitQuotaProvider _quotaProvider = Substitute.For<IRateLimitQuotaProvider>();
    private readonly IMeterFactory _meterFactory;

    public RateLimitMiddlewareTests()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        ServiceProvider sp = services.BuildServiceProvider();
        _meterFactory = sp.GetRequiredService<IMeterFactory>();
    }

    public void Dispose() =>
        (_meterFactory as IDisposable)?.Dispose();

    private TenantPartitionedRateLimiter CreateLimiter(GranitRateLimitingOptions? options = null)
    {
        options ??= new GranitRateLimitingOptions
        {
            Enabled = true,
            Policies = new Dictionary<string, RateLimitPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["api"] = new() { PermitLimit = 100 },
            },
        };

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(false);
        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();

        return new TenantPartitionedRateLimiter(
            _counterStore,
            _quotaProvider,
            MsOptions.Create(options),
            currentTenant,
            currentUser,
            new RateLimitingMetrics(_meterFactory),
            NullLogger<TenantPartitionedRateLimiter>.Instance);
    }

    [Fact]
    public async Task BeforeAsync_NoAttribute_DoesNotCallCounterStore()
    {
        TenantPartitionedRateLimiter limiter = CreateLimiter();

        await RateLimitMiddleware.BeforeAsync(
            new PlainMessage(),
            limiter,
            TestContext.Current.CancellationToken);

        await _counterStore.DidNotReceiveWithAnyArgs()
            .CheckAndIncrementAsync(default!, default, default, default, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task BeforeAsync_Allowed_DoesNotThrow()
    {
        _quotaProvider.GetPermitLimitAsync("api", Arg.Any<CancellationToken>())
            .Returns((int?)null);
        _counterStore.CheckAndIncrementAsync(
                Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(),
                Arg.Any<RateLimitAlgorithm>(), Arg.Any<RateLimitPolicyOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(new RateLimitResult(true, 99, 100, TimeSpan.Zero));

        TenantPartitionedRateLimiter limiter = CreateLimiter();

        Func<Task> act = () => RateLimitMiddleware.BeforeAsync(
            new RateLimitedMessage(),
            limiter,
            TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task BeforeAsync_Rejected_ThrowsRateLimitExceededException()
    {
        _quotaProvider.GetPermitLimitAsync("api", Arg.Any<CancellationToken>())
            .Returns((int?)null);
        _counterStore.CheckAndIncrementAsync(
                Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(),
                Arg.Any<RateLimitAlgorithm>(), Arg.Any<RateLimitPolicyOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(new RateLimitResult(false, 0, 100, TimeSpan.FromSeconds(30)));

        TenantPartitionedRateLimiter limiter = CreateLimiter();

        Func<Task> act = () => RateLimitMiddleware.BeforeAsync(
            new RateLimitedMessage(),
            limiter,
            TestContext.Current.CancellationToken);

        RateLimitExceededException ex = await Should.ThrowAsync<RateLimitExceededException>(act);
        ex.PolicyName.ShouldBe("api");
        ex.RetryAfter.ShouldBe(TimeSpan.FromSeconds(30));
        ex.Limit.ShouldBe(100);
        ex.Remaining.ShouldBe(0);
    }
}
