using Granit.Caching.Options;
using Shouldly;
using Xunit;

namespace Granit.Caching.FusionCache.Tests;

public sealed class FusionCachingOptionsTests
{
    [Fact]
    public void Defaults_AreProductionReady()
    {
        var options = new FusionCachingOptions();

        options.FailSafeIsEnabled.ShouldBeTrue();
        options.FailSafeMaxDuration.ShouldBe(TimeSpan.FromHours(2));
        options.FailSafeThrottleDuration.ShouldBe(TimeSpan.FromSeconds(30));
        options.FactorySoftTimeout.ShouldBe(TimeSpan.FromSeconds(2));
        options.FactoryHardTimeout.ShouldBe(TimeSpan.FromSeconds(10));
        options.EagerRefreshThreshold.ShouldBe(0.8f);
        options.BackplaneChannelPrefix.ShouldBe("granit:fc");
    }

    [Fact]
    public void SectionName_IsCorrect()
    {
        FusionCachingOptions.SectionName.ShouldBe("Cache:FusionCache");
    }
}
