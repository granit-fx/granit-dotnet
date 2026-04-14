using System.Diagnostics.Metrics;
using Granit.MultiTenancy;
using Granit.RateLimiting.Abstractions;
using Granit.RateLimiting.Diagnostics;
using Granit.RateLimiting.Internal;
using Granit.RateLimiting.Options;
using Granit.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.RateLimiting.Tests;

public sealed class TenantPartitionedRateLimiterTests : IDisposable
{
    private readonly IRateLimitCounterStore _counterStore = Substitute.For<IRateLimitCounterStore>();
    private readonly IRateLimitQuotaProvider _quotaProvider = Substitute.For<IRateLimitQuotaProvider>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IMeterFactory _meterFactory;

    private readonly GranitRateLimitingOptions _options = new()
    {
        Enabled = true,
        KeyPrefix = "rl",
        Policies = new Dictionary<string, RateLimitPolicyOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["api"] = new() { PermitLimit = 100, Window = TimeSpan.FromMinutes(1) },
        },
    };

    public TenantPartitionedRateLimiterTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        ServiceProvider sp = services.BuildServiceProvider();
        _meterFactory = sp.GetRequiredService<IMeterFactory>();
    }

    public void Dispose() =>
        (_meterFactory as IDisposable)?.Dispose();

    private TenantPartitionedRateLimiter CreateLimiter() =>
        new(
            _counterStore,
            _quotaProvider,
            MsOptions.Create(_options),
            _currentTenant,
            _currentUser,
            new RateLimitingMetrics(_meterFactory),
            NullLogger<TenantPartitionedRateLimiter>.Instance);

    // =========================================================================
    // Disabled
    // =========================================================================

    [Fact]
    public async Task CheckAsync_Disabled_ReturnsNull()
    {
        _options.Enabled = false;
        TenantPartitionedRateLimiter limiter = CreateLimiter();

        RateLimitResult? result = await limiter.CheckAsync("api", clientIp: null, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        await _counterStore.DidNotReceiveWithAnyArgs()
            .CheckAndIncrementAsync(default!, default, default, default, default!, TestContext.Current.CancellationToken);
    }

    // =========================================================================
    // Unknown policy
    // =========================================================================

    [Fact]
    public async Task CheckAsync_UnknownPolicy_ReturnsNull()
    {
        TenantPartitionedRateLimiter limiter = CreateLimiter();

        RateLimitResult? result = await limiter.CheckAsync("nonexistent", clientIp: null, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // =========================================================================
    // Bypass roles
    // =========================================================================

    [Fact]
    public async Task CheckAsync_UserHasBypassRole_ReturnsNull()
    {
        _options.BypassRoles = ["Admin"];
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.IsInRole("Admin").Returns(true);
        TenantPartitionedRateLimiter limiter = CreateLimiter();

        RateLimitResult? result = await limiter.CheckAsync("api", clientIp: null, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task CheckAsync_UserWithoutBypassRole_ProceedsToCheck()
    {
        _options.BypassRoles = ["Admin"];
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.IsInRole("Admin").Returns(false);

        _quotaProvider.GetPermitLimitAsync("api", Arg.Any<CancellationToken>())
            .Returns((int?)null);
        _counterStore.CheckAndIncrementAsync(
                Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(),
                Arg.Any<RateLimitAlgorithm>(), Arg.Any<RateLimitPolicyOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(new RateLimitResult(true, 99, 100, TimeSpan.Zero));

        TenantPartitionedRateLimiter limiter = CreateLimiter();
        RateLimitResult? result = await limiter.CheckAsync("api", clientIp: null, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsAllowed.ShouldBeTrue();
    }

    [Fact]
    public async Task CheckAsync_UnauthenticatedUser_DoesNotCheckBypassRoles()
    {
        _options.BypassRoles = ["Admin"];
        _currentUser.IsAuthenticated.Returns(false);

        _quotaProvider.GetPermitLimitAsync("api", Arg.Any<CancellationToken>())
            .Returns((int?)null);
        _counterStore.CheckAndIncrementAsync(
                Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(),
                Arg.Any<RateLimitAlgorithm>(), Arg.Any<RateLimitPolicyOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(new RateLimitResult(true, 99, 100, TimeSpan.Zero));

        TenantPartitionedRateLimiter limiter = CreateLimiter();
        RateLimitResult? result = await limiter.CheckAsync("api", clientIp: null, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        _currentUser.DidNotReceiveWithAnyArgs().IsInRole(default!);
    }

    // =========================================================================
    // Tenant key partitioning
    // =========================================================================

    [Fact]
    public async Task CheckAsync_WithTenant_IncludesTenantInKey()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);

        _quotaProvider.GetPermitLimitAsync("api", Arg.Any<CancellationToken>())
            .Returns((int?)null);
        _counterStore.CheckAndIncrementAsync(
                Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(),
                Arg.Any<RateLimitAlgorithm>(), Arg.Any<RateLimitPolicyOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(new RateLimitResult(true, 99, 100, TimeSpan.Zero));

        TenantPartitionedRateLimiter limiter = CreateLimiter();
        await limiter.CheckAsync("api", clientIp: null, TestContext.Current.CancellationToken);

        await _counterStore.Received(1).CheckAndIncrementAsync(
            Arg.Is<string>(k => k.Contains(tenantId.ToString())),
            Arg.Any<int>(), Arg.Any<TimeSpan>(),
            Arg.Any<RateLimitAlgorithm>(), Arg.Any<RateLimitPolicyOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAsync_WithoutTenant_UsesGlobalKey()
    {
        _currentTenant.IsAvailable.Returns(false);

        _quotaProvider.GetPermitLimitAsync("api", Arg.Any<CancellationToken>())
            .Returns((int?)null);
        _counterStore.CheckAndIncrementAsync(
                Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(),
                Arg.Any<RateLimitAlgorithm>(), Arg.Any<RateLimitPolicyOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(new RateLimitResult(true, 99, 100, TimeSpan.Zero));

        TenantPartitionedRateLimiter limiter = CreateLimiter();
        await limiter.CheckAsync("api", clientIp: null, TestContext.Current.CancellationToken);

        await _counterStore.Received(1).CheckAndIncrementAsync(
            Arg.Is<string>(k => k.Contains("{global}")),
            Arg.Any<int>(), Arg.Any<TimeSpan>(),
            Arg.Any<RateLimitAlgorithm>(), Arg.Any<RateLimitPolicyOptions>(),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Quota provider integration
    // =========================================================================

    [Fact]
    public async Task CheckAsync_QuotaProviderReturnsLimit_UsesIt()
    {
        _currentTenant.IsAvailable.Returns(false);
        _quotaProvider.GetPermitLimitAsync("api", Arg.Any<CancellationToken>())
            .Returns(50);

        _counterStore.CheckAndIncrementAsync(
                Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(),
                Arg.Any<RateLimitAlgorithm>(), Arg.Any<RateLimitPolicyOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(new RateLimitResult(true, 49, 50, TimeSpan.Zero));

        TenantPartitionedRateLimiter limiter = CreateLimiter();
        await limiter.CheckAsync("api", clientIp: null, TestContext.Current.CancellationToken);

        await _counterStore.Received(1).CheckAndIncrementAsync(
            Arg.Any<string>(),
            50,
            Arg.Any<TimeSpan>(),
            Arg.Any<RateLimitAlgorithm>(), Arg.Any<RateLimitPolicyOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAsync_QuotaProviderReturnsNull_FallsBackToStaticLimit()
    {
        _currentTenant.IsAvailable.Returns(false);
        _quotaProvider.GetPermitLimitAsync("api", Arg.Any<CancellationToken>())
            .Returns((int?)null);

        _counterStore.CheckAndIncrementAsync(
                Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(),
                Arg.Any<RateLimitAlgorithm>(), Arg.Any<RateLimitPolicyOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(new RateLimitResult(true, 99, 100, TimeSpan.Zero));

        TenantPartitionedRateLimiter limiter = CreateLimiter();
        await limiter.CheckAsync("api", clientIp: null, TestContext.Current.CancellationToken);

        await _counterStore.Received(1).CheckAndIncrementAsync(
            Arg.Any<string>(),
            100, // static config from options
            Arg.Any<TimeSpan>(),
            Arg.Any<RateLimitAlgorithm>(), Arg.Any<RateLimitPolicyOptions>(),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Allowed / Rejected results
    // =========================================================================

    [Fact]
    public async Task CheckAsync_Allowed_ReturnsResult()
    {
        _currentTenant.IsAvailable.Returns(false);
        _quotaProvider.GetPermitLimitAsync("api", Arg.Any<CancellationToken>())
            .Returns((int?)null);
        _counterStore.CheckAndIncrementAsync(
                Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(),
                Arg.Any<RateLimitAlgorithm>(), Arg.Any<RateLimitPolicyOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(new RateLimitResult(true, 99, 100, TimeSpan.Zero));

        TenantPartitionedRateLimiter limiter = CreateLimiter();
        RateLimitResult? result = await limiter.CheckAsync("api", clientIp: null, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsAllowed.ShouldBeTrue();
    }

    [Fact]
    public async Task CheckAsync_Rejected_ReturnsResult()
    {
        _currentTenant.IsAvailable.Returns(false);
        _quotaProvider.GetPermitLimitAsync("api", Arg.Any<CancellationToken>())
            .Returns((int?)null);
        var retryAfter = TimeSpan.FromSeconds(30);
        _counterStore.CheckAndIncrementAsync(
                Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(),
                Arg.Any<RateLimitAlgorithm>(), Arg.Any<RateLimitPolicyOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(new RateLimitResult(false, 0, 100, retryAfter));

        TenantPartitionedRateLimiter limiter = CreateLimiter();
        RateLimitResult? result = await limiter.CheckAsync("api", clientIp: null, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsAllowed.ShouldBeFalse();
        result.RetryAfter.ShouldBe(retryAfter);
    }
}
