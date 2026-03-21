using Granit.Caching.Options;
using Granit.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Caching.Tests;

public sealed class GranitCachingModuleAdditionalTests
{
    private static ServiceProvider BuildServiceProvider(
        Action<HostApplicationBuilder>? configure = null)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        configure?.Invoke(builder);
        GranitCachingModule module = new();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        module.ConfigureServices(context);
        return builder.Services.BuildServiceProvider();
    }

    [Fact]
    public void ConfigureServices_RegistersFusionCachingOptions()
    {
        // Arrange & Act
        using ServiceProvider sp = BuildServiceProvider();

        // Assert
        IOptions<FusionCachingOptions> options = sp.GetRequiredService<IOptions<FusionCachingOptions>>();
        options.Value.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureServices_RegistersFusionCache()
    {
        // Arrange & Act
        using ServiceProvider sp = BuildServiceProvider();

        // Assert
        IFusionCache cache = sp.GetRequiredService<IFusionCache>();
        cache.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureServices_CachingOptions_ReflectsConfiguration()
    {
        // Arrange & Act
        using ServiceProvider sp = BuildServiceProvider(builder =>
        {
            builder.Configuration["Cache:KeyPrefix"] = "custom";
            builder.Configuration["Cache:EncryptValues"] = "true";
        });

        // Assert
        CachingOptions options = sp.GetRequiredService<IOptions<CachingOptions>>().Value;
        options.KeyPrefix.ShouldBe("custom");
        options.EncryptValues.ShouldBeTrue();
    }

    [Fact]
    public void DependsOn_GranitTimingModule()
    {
        // Assert — verify [DependsOn] attribute references GranitTimingModule
        var attr = (DependsOnAttribute?)Attribute.GetCustomAttribute(
            typeof(GranitCachingModule), typeof(DependsOnAttribute));

        attr.ShouldNotBeNull();
        attr!.DependedTypes.ShouldContain(typeof(Granit.Timing.GranitTimingModule));
    }

    [Fact]
    public void Module_InheritsFromGranitModule()
    {
        GranitCachingModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }
}
