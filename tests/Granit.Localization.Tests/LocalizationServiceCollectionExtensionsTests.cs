using Granit.Localization.Extensions;
using Granit.Localization.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Localization.Tests;

public sealed class LocalizationServiceCollectionExtensionsTests
{
    private static ServiceCollection CreateServices()
    {
        ServiceCollection services = new();
        services.AddOptions();
        services.AddSingleton<IFusionCache>(new FusionCache(new FusionCacheOptions()));
        return services;
    }

    [Fact]
    public void AddGranitLocalization_RegistersStringLocalizerFactory()
    {
        ServiceCollection services = CreateServices();

        services.AddGranitLocalization();
        using ServiceProvider sp = services.BuildServiceProvider();

        IStringLocalizerFactory? factory = sp.GetService<IStringLocalizerFactory>();
        factory.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitLocalization_RegistersGenericStringLocalizer()
    {
        ServiceCollection services = CreateServices();

        services.AddGranitLocalization();
        using ServiceProvider sp = services.BuildServiceProvider();

        IStringLocalizer<GranitLocalizationResource>? localizer =
            sp.GetService<IStringLocalizer<GranitLocalizationResource>>();
        localizer.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitLocalization_RegistersOverrideStoreReader()
    {
        ServiceCollection services = CreateServices();

        services.AddGranitLocalization();
        using ServiceProvider sp = services.BuildServiceProvider();

        ILocalizationOverrideStoreReader? reader = sp.GetService<ILocalizationOverrideStoreReader>();
        reader.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitLocalization_RegistersOverrideStoreWriter()
    {
        ServiceCollection services = CreateServices();

        services.AddGranitLocalization();
        using ServiceProvider sp = services.BuildServiceProvider();

        ILocalizationOverrideStoreWriter? writer = sp.GetService<ILocalizationOverrideStoreWriter>();
        writer.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitLocalization_WithConfigure_AppliesOptions()
    {
        ServiceCollection services = CreateServices();

        services.AddGranitLocalization(options =>
        {
            options.EnableAutoDiscovery = true;
            options.Languages.Add(new LanguageInfo("fr", "Français", "fr"));
        });
        using ServiceProvider sp = services.BuildServiceProvider();

        GranitLocalizationOptions options =
            sp.GetRequiredService<IOptions<GranitLocalizationOptions>>().Value;
        options.EnableAutoDiscovery.ShouldBeTrue();
        options.Languages.Count.ShouldBe(1);
    }

    [Fact]
    public void AddGranitLocalization_WithoutConfigure_DoesNotThrow()
    {
        ServiceCollection services = CreateServices();

        Should.NotThrow(() => services.AddGranitLocalization());
    }

    [Fact]
    public void AddGranitLocalization_ReturnsServiceCollection()
    {
        ServiceCollection services = CreateServices();

        IServiceCollection result = services.AddGranitLocalization();
        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void ConfigureLocalizationOverridesCache_AppliesCacheTtl()
    {
        ServiceCollection services = CreateServices();
        services.AddGranitLocalization();

        services.ConfigureLocalizationOverridesCache(options => options.CacheTtl = TimeSpan.FromMinutes(10));

        using ServiceProvider sp = services.BuildServiceProvider();
        LocalizationOverridesCacheOptions cacheOptions =
            sp.GetRequiredService<IOptions<LocalizationOverridesCacheOptions>>().Value;

        cacheOptions.CacheTtl.ShouldBe(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void ConfigureLocalizationOverridesCache_ReturnsServiceCollection()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.ConfigureLocalizationOverridesCache(options => options.CacheTtl = TimeSpan.FromMinutes(1));

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitLocalization_CalledTwice_DoesNotDuplicateRegistrations()
    {
        ServiceCollection services = CreateServices();

        services.AddGranitLocalization();
        services.AddGranitLocalization();

        using ServiceProvider sp = services.BuildServiceProvider();

        // TryAdd* should prevent duplicates
        IEnumerable<IStringLocalizerFactory> factories =
            sp.GetServices<IStringLocalizerFactory>();
        factories.Count().ShouldBe(1);
    }
}
