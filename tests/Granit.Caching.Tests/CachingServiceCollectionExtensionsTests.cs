using Granit.Caching.Extensions;
using Granit.Caching.Options;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace Granit.Caching.Tests;

public sealed class CachingServiceCollectionExtensionsTests
{
    private static ServiceProvider BuildServiceProvider(
        Action<HostApplicationBuilder>? configure = null)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        configure?.Invoke(builder);
        builder.Services.AddGranitCaching();
        return builder.Services.BuildServiceProvider();
    }

    [Fact]
    public void AddGranitCaching_RegistersFusionCachingOptions()
    {
        using ServiceProvider sp = BuildServiceProvider();

        IOptions<FusionCachingOptions> options = sp.GetRequiredService<IOptions<FusionCachingOptions>>();

        options.Value.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitCaching_RegistersDistributedMemoryCache()
    {
        using ServiceProvider sp = BuildServiceProvider();

        IDistributedCache cache = sp.GetRequiredService<IDistributedCache>();

        cache.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitCaching_RegistersFusionCache()
    {
        using ServiceProvider sp = BuildServiceProvider();

        IFusionCache cache = sp.GetRequiredService<IFusionCache>();

        cache.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitCaching_RegistersEncryptingSerializer()
    {
        using ServiceProvider sp = BuildServiceProvider();

        IFusionCacheSerializer serializer = sp.GetRequiredService<IFusionCacheSerializer>();

        serializer.ShouldNotBeNull();
        serializer.GetType().Name.ShouldBe("EncryptingFusionCacheSerializer");
    }

    [Fact]
    public void AddGranitCaching_FusionCacheOptions_UsesDefaultDuration()
    {
        using ServiceProvider sp = BuildServiceProvider();

        FusionCacheOptions fcOptions = sp.GetRequiredService<IOptions<FusionCacheOptions>>().Value;

        fcOptions.DefaultEntryOptions.Duration.ShouldBe(TimeSpan.FromHours(1));
    }

    [Fact]
    public void AddGranitCaching_FusionCacheOptions_UsesCustomDuration()
    {
        using ServiceProvider sp = BuildServiceProvider(builder =>
        {
            builder.Configuration["Cache:DefaultAbsoluteExpirationRelativeToNow"] = "00:30:00";
        });

        FusionCacheOptions fcOptions = sp.GetRequiredService<IOptions<FusionCacheOptions>>().Value;

        fcOptions.DefaultEntryOptions.Duration.ShouldBe(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void AddGranitCaching_FusionCacheOptions_SetsFailSafeFromFusionOptions()
    {
        using ServiceProvider sp = BuildServiceProvider();

        FusionCacheOptions fcOptions = sp.GetRequiredService<IOptions<FusionCacheOptions>>().Value;

        fcOptions.DefaultEntryOptions.IsFailSafeEnabled.ShouldBeTrue();
        fcOptions.DefaultEntryOptions.FailSafeMaxDuration.ShouldBe(TimeSpan.FromHours(2));
        fcOptions.DefaultEntryOptions.FailSafeThrottleDuration.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void AddGranitCaching_FusionCacheOptions_SetsFactoryTimeouts()
    {
        using ServiceProvider sp = BuildServiceProvider();

        FusionCacheOptions fcOptions = sp.GetRequiredService<IOptions<FusionCacheOptions>>().Value;

        fcOptions.DefaultEntryOptions.FactorySoftTimeout.ShouldBe(TimeSpan.FromSeconds(2));
        fcOptions.DefaultEntryOptions.FactoryHardTimeout.ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void AddGranitCaching_FusionCacheOptions_SetsEagerRefreshThreshold()
    {
        using ServiceProvider sp = BuildServiceProvider();

        FusionCacheOptions fcOptions = sp.GetRequiredService<IOptions<FusionCacheOptions>>().Value;

        fcOptions.DefaultEntryOptions.EagerRefreshThreshold.ShouldBe(0.8f);
    }

    [Fact]
    public void AddGranitCaching_FusionCacheOptions_SetsCacheKeyPrefix()
    {
        using ServiceProvider sp = BuildServiceProvider();

        FusionCacheOptions fcOptions = sp.GetRequiredService<IOptions<FusionCacheOptions>>().Value;

        fcOptions.CacheKeyPrefix.ShouldBe("dd:");
    }

    [Fact]
    public void AddGranitCaching_FusionCacheOptions_SetsCustomKeyPrefix()
    {
        using ServiceProvider sp = BuildServiceProvider(builder =>
        {
            builder.Configuration["Cache:KeyPrefix"] = "myapp";
        });

        FusionCacheOptions fcOptions = sp.GetRequiredService<IOptions<FusionCacheOptions>>().Value;

        fcOptions.CacheKeyPrefix.ShouldBe("myapp:");
    }

    [Fact]
    public void AddGranitCaching_FusionCacheOptions_SetsBackplaneChannelPrefix()
    {
        using ServiceProvider sp = BuildServiceProvider();

        FusionCacheOptions fcOptions = sp.GetRequiredService<IOptions<FusionCacheOptions>>().Value;

        fcOptions.BackplaneChannelPrefix.ShouldBe("granit:fc");
    }

    [Fact]
    public void AddGranitCaching_FusionCacheOptions_EnablesAutoRecovery()
    {
        using ServiceProvider sp = BuildServiceProvider();

        FusionCacheOptions fcOptions = sp.GetRequiredService<IOptions<FusionCacheOptions>>().Value;

        fcOptions.EnableAutoRecovery.ShouldBeTrue();
    }
}
