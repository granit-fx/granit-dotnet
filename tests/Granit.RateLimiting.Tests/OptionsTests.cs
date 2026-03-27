using Granit.RateLimiting.Options;
using Shouldly;
using Xunit;

namespace Granit.RateLimiting.Tests;

public sealed class OptionsTests
{
    [Fact]
    public void GranitRateLimitingOptions_DefaultValues()
    {
        var options = new GranitRateLimitingOptions();

        options.Enabled.ShouldBeTrue();
        options.KeyPrefix.ShouldBe("rl");
        options.FallbackOnCounterStoreFailure.ShouldBe(CounterStoreFailureBehavior.Deny);
        options.BypassRoles.ShouldBeEmpty();
        options.Policies.ShouldBeEmpty();
        options.UseFeatureBasedQuotas.ShouldBeFalse();
    }

    [Fact]
    public void RateLimitPolicyOptions_DefaultValues()
    {
        var policy = new RateLimitPolicyOptions();

        policy.Algorithm.ShouldBe(RateLimitAlgorithm.SlidingWindow);
        policy.PermitLimit.ShouldBe(1000);
        policy.Window.ShouldBe(TimeSpan.FromMinutes(1));
        policy.SegmentsPerWindow.ShouldBe(6);
        policy.TokenLimit.ShouldBe(50);
        policy.TokensPerPeriod.ShouldBe(10);
        policy.ReplenishmentPeriod.ShouldBe(TimeSpan.FromSeconds(10));
        policy.PartitionBy.ShouldBe(RateLimitPartition.Tenant);
        policy.FeatureName.ShouldBeNull();
    }

    [Fact]
    public void Policies_CaseInsensitive()
    {
        var options = new GranitRateLimitingOptions
        {
            Policies = new Dictionary<string, RateLimitPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["Api"] = new() { PermitLimit = 100 },
            },
        };

        options.Policies.TryGetValue("api", out RateLimitPolicyOptions? policy).ShouldBeTrue();
        policy!.PermitLimit.ShouldBe(100);
    }

    [Fact]
    public void SectionName_IsRateLimiting() =>
        GranitRateLimitingOptions.SectionName.ShouldBe("RateLimiting");
}
