using Granit.Features;
using Granit.Features.Exceptions;
using Granit.RateLimiting.Internal;
using Granit.RateLimiting.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.RateLimiting.Tests;

public sealed class FeatureBasedRateLimitQuotaProviderTests
{
    private GranitRateLimitingOptions _options = new()
    {
        Policies = new Dictionary<string, RateLimitPolicyOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["api"] = new() { PermitLimit = 100 },
        },
    };

    private TestOptionsMonitor CreateMonitor() => new(_options);

    [Fact]
    public async Task GetPermitLimitAsync_PolicyNotFound_ReturnsNull()
    {
        ServiceCollection services = new();
        ServiceProvider sp = services.BuildServiceProvider();

        FeatureBasedRateLimitQuotaProvider provider = new(CreateMonitor(), sp);

        int? result = await provider.GetPermitLimitAsync("nonexistent", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetPermitLimitAsync_NoFeatureChecker_FallsBackToStaticConfig()
    {
        ServiceCollection services = new();
        ServiceProvider sp = services.BuildServiceProvider();

        FeatureBasedRateLimitQuotaProvider provider = new(CreateMonitor(), sp);

        int? result = await provider.GetPermitLimitAsync("api", TestContext.Current.CancellationToken);

        result.ShouldBe(100);
    }

    [Fact]
    public async Task GetPermitLimitAsync_FeatureCheckerReturnsValue_UsesFeatureValue()
    {
        IFeatureChecker featureChecker = Substitute.For<IFeatureChecker>();
        featureChecker.GetNumericAsync("RateLimit.api", Arg.Any<CancellationToken>())
            .Returns(500L);

        ServiceCollection services = new();
        services.AddSingleton(featureChecker);
        ServiceProvider sp = services.BuildServiceProvider();

        FeatureBasedRateLimitQuotaProvider provider = new(CreateMonitor(), sp);

        int? result = await provider.GetPermitLimitAsync("api", TestContext.Current.CancellationToken);

        result.ShouldBe(500);
    }

    [Fact]
    public async Task GetPermitLimitAsync_FeatureCheckerReturnsZero_FallsBackToStaticConfig()
    {
        IFeatureChecker featureChecker = Substitute.For<IFeatureChecker>();
        featureChecker.GetNumericAsync("RateLimit.api", Arg.Any<CancellationToken>())
            .Returns(0L);

        ServiceCollection services = new();
        services.AddSingleton(featureChecker);
        ServiceProvider sp = services.BuildServiceProvider();

        FeatureBasedRateLimitQuotaProvider provider = new(CreateMonitor(), sp);

        int? result = await provider.GetPermitLimitAsync("api", TestContext.Current.CancellationToken);

        result.ShouldBe(100);
    }

    [Fact]
    public async Task GetPermitLimitAsync_FeatureNotFound_FallsBackToStaticConfig()
    {
        IFeatureChecker featureChecker = Substitute.For<IFeatureChecker>();
        featureChecker.GetNumericAsync("RateLimit.api", Arg.Any<CancellationToken>())
            .Throws(new FeatureNotFoundException("RateLimit.api"));

        ServiceCollection services = new();
        services.AddSingleton(featureChecker);
        ServiceProvider sp = services.BuildServiceProvider();

        FeatureBasedRateLimitQuotaProvider provider = new(CreateMonitor(), sp);

        int? result = await provider.GetPermitLimitAsync("api", TestContext.Current.CancellationToken);

        result.ShouldBe(100);
    }

    [Fact]
    public async Task GetPermitLimitAsync_CustomFeatureName_UsesOverride()
    {
        _options = new GranitRateLimitingOptions
        {
            Policies = new Dictionary<string, RateLimitPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["api"] = new() { PermitLimit = 100, FeatureName = "CustomQuota.Api" },
            },
        };

        IFeatureChecker featureChecker = Substitute.For<IFeatureChecker>();
        featureChecker.GetNumericAsync("CustomQuota.Api", Arg.Any<CancellationToken>())
            .Returns(750L);

        ServiceCollection services = new();
        services.AddSingleton(featureChecker);
        ServiceProvider sp = services.BuildServiceProvider();

        FeatureBasedRateLimitQuotaProvider provider = new(CreateMonitor(), sp);

        int? result = await provider.GetPermitLimitAsync("api", TestContext.Current.CancellationToken);

        result.ShouldBe(750);
    }

    [Fact]
    public async Task GetPermitLimitAsync_NegativeFeatureValue_FallsBackToStaticConfig()
    {
        IFeatureChecker featureChecker = Substitute.For<IFeatureChecker>();
        featureChecker.GetNumericAsync("RateLimit.api", Arg.Any<CancellationToken>())
            .Returns(-5L);

        ServiceCollection services = new();
        services.AddSingleton(featureChecker);
        ServiceProvider sp = services.BuildServiceProvider();

        FeatureBasedRateLimitQuotaProvider provider = new(CreateMonitor(), sp);

        int? result = await provider.GetPermitLimitAsync("api", TestContext.Current.CancellationToken);

        result.ShouldBe(100);
    }

    private sealed class TestOptionsMonitor(GranitRateLimitingOptions options) : IOptionsMonitor<GranitRateLimitingOptions>
    {
        public GranitRateLimitingOptions CurrentValue => options;

        public GranitRateLimitingOptions Get(string? name) => options;

        public IDisposable? OnChange(Action<GranitRateLimitingOptions, string?> listener) => null;
    }
}
