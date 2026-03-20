using Granit.Caching;
using Granit.Core.Modularity;
using Granit.Features.Definitions;
using Granit.Features.ValueProviders;
using Granit.Localization;
using Granit.Localization.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Features.Tests;

public sealed class GranitFeaturesModuleTests
{
    // -------------------------------------------------------------------------
    // Module metadata — DependsOn attributes
    // -------------------------------------------------------------------------

    [Fact]
    public void Module_DependsOn_GranitCachingModule()
    {
        var attributes = (DependsOnAttribute[])Attribute.GetCustomAttributes(
            typeof(GranitFeaturesModule), typeof(DependsOnAttribute));

        attributes.SelectMany(a => a.DependedTypes)
                  .ShouldContain(typeof(GranitCachingModule));
    }

    [Fact]
    public void Module_DependsOn_GranitLocalizationModule()
    {
        var attributes = (DependsOnAttribute[])Attribute.GetCustomAttributes(
            typeof(GranitFeaturesModule), typeof(DependsOnAttribute));

        attributes.SelectMany(a => a.DependedTypes)
                  .ShouldContain(typeof(GranitLocalizationModule));
    }

    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitFeaturesModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsFrom_GranitModule() =>
        typeof(GranitFeaturesModule).IsSubclassOf(typeof(GranitModule)).ShouldBeTrue();

    // -------------------------------------------------------------------------
    // ConfigureServices — registers feature infrastructure
    // -------------------------------------------------------------------------

    [Fact]
    public void ConfigureServices_Registers_IFeatureDefinitionStore()
    {
        ServiceCollection services = BuildServicesViaModule();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IFeatureDefinitionStore) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void ConfigureServices_Registers_IFeatureStoreReader()
    {
        ServiceCollection services = BuildServicesViaModule();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IFeatureStoreReader) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void ConfigureServices_Registers_IFeatureStoreWriter()
    {
        ServiceCollection services = BuildServicesViaModule();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IFeatureStoreWriter) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void ConfigureServices_Registers_IFeatureChecker()
    {
        ServiceCollection services = BuildServicesViaModule();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IFeatureChecker) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void ConfigureServices_Registers_IFeatureLimitGuard()
    {
        ServiceCollection services = BuildServicesViaModule();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IFeatureLimitGuard) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void ConfigureServices_Registers_Three_ValueProviders()
    {
        ServiceCollection services = BuildServicesViaModule();

        int count = services.Count(d => d.ServiceType == typeof(IFeatureValueProvider));
        count.ShouldBe(3);
    }

    [Fact]
    public void ConfigureServices_ConfiguresLocalizationOptions()
    {
        ServiceCollection services = BuildServicesViaModule();

        // The module registers a Configure<GranitLocalizationOptions> call
        services.ShouldContain(d =>
            d.ServiceType == typeof(Microsoft.Extensions.Options.IConfigureOptions<GranitLocalizationOptions>));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ServiceCollection BuildServicesViaModule()
    {
        ServiceCollection services = new();
        GranitFeaturesModule module = new();

        ConfigurationBuilder configBuilder = new();
        IConfiguration configuration = configBuilder.Build();

        IHostApplicationBuilder hostBuilder = Substitute.For<IHostApplicationBuilder>();

        ServiceConfigurationContext context = new(services, configuration, hostBuilder);
        module.ConfigureServices(context);

        return services;
    }
}
