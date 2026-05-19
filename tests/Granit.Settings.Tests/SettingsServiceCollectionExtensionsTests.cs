using Granit.Settings.Definitions;
using Granit.Settings.Extensions;
using Granit.Settings.Options;
using Granit.Settings.Providers;
using Granit.Settings.Services;
using Granit.Settings.Stores;
using Granit.Settings.Values;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Settings.Tests;

public sealed class SettingsServiceCollectionExtensionsTests
{
    private static ServiceProvider BuildProvider(IConfigurationSection? section = null)
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitSettings(section);

        // Register dependencies that come from other modules in production
        services.AddSingleton(Substitute.For<Granit.Users.ICurrentUserService>());
        services.AddSingleton(Substitute.For<Granit.MultiTenancy.ICurrentTenant>());
        services.AddFusionCache();
        services.AddOptions();
        services.AddLogging();
        services.AddMetrics();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddGranitSettings_Registers_SettingDefinitionManager()
    {
        using ServiceProvider sp = BuildProvider();

        SettingDefinitionManager manager = sp.GetRequiredService<SettingDefinitionManager>();

        manager.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitSettings_Registers_InMemorySettingStore_ForBothInterfaces()
    {
        using ServiceProvider sp = BuildProvider();

        ISettingStoreReader reader = sp.GetRequiredService<ISettingStoreReader>();
        ISettingStoreWriter writer = sp.GetRequiredService<ISettingStoreWriter>();

        reader.ShouldBeOfType<InMemorySettingStore>();
        writer.ShouldBeOfType<InMemorySettingStore>();
        reader.ShouldBeSameAs(writer);
    }

    [Fact]
    public void AddGranitSettings_Registers_FiveProviders()
    {
        using ServiceProvider sp = BuildProvider();
        using IServiceScope scope = sp.CreateScope();

        IEnumerable<ISettingValueProvider> providers =
            scope.ServiceProvider.GetServices<ISettingValueProvider>();

        providers.Count().ShouldBe(5);
    }

    [Fact]
    public void AddGranitSettings_Registers_ISettingProvider()
    {
        using ServiceProvider sp = BuildProvider();
        using IServiceScope scope = sp.CreateScope();

        ISettingProvider provider = scope.ServiceProvider.GetRequiredService<ISettingProvider>();

        provider.ShouldBeOfType<SettingProvider>();
    }

    [Fact]
    public void AddGranitSettings_Registers_ISettingManager()
    {
        using ServiceProvider sp = BuildProvider();
        using IServiceScope scope = sp.CreateScope();

        ISettingManager manager = scope.ServiceProvider.GetRequiredService<ISettingManager>();

        manager.ShouldBeOfType<SettingManager>();
    }

    [Fact]
    public void AddGranitSettings_WithConfiguration_BindsOptions()
    {
        Dictionary<string, string?> configData = new()
        {
            ["Settings:CacheExpiration"] = "01:00:00",
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        IConfigurationSection section = configuration.GetSection("Settings");

        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddGranitSettings(section);
        services.AddSingleton(Substitute.For<Granit.Users.ICurrentUserService>());
        services.AddSingleton(Substitute.For<Granit.MultiTenancy.ICurrentTenant>());
        services.AddFusionCache();
        services.AddOptions();
        services.AddLogging();

        using ServiceProvider sp = services.BuildServiceProvider();

        SettingsOptions options = sp.GetRequiredService<IOptions<SettingsOptions>>().Value;

        options.CacheExpiration.ShouldBe(TimeSpan.FromHours(1));
    }

    [Fact]
    public void AddGranitSettings_WithoutConfiguration_UsesDefaults()
    {
        using ServiceProvider sp = BuildProvider();

        SettingsOptions options = sp.GetRequiredService<IOptions<SettingsOptions>>().Value;

        options.CacheExpiration.ShouldBe(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void AddGranitSettings_IsIdempotent()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitSettings();
        services.AddGranitSettings();
        services.AddSingleton(Substitute.For<Granit.Users.ICurrentUserService>());
        services.AddSingleton(Substitute.For<Granit.MultiTenancy.ICurrentTenant>());
        services.AddFusionCache();
        services.AddOptions();
        services.AddLogging();

        using ServiceProvider sp = services.BuildServiceProvider();

        SettingDefinitionManager manager = sp.GetRequiredService<SettingDefinitionManager>();
        manager.ShouldNotBeNull();
    }
}
