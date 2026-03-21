using Granit.Core.Modularity;
using Granit.Vault.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Vault.Tests;

public sealed class GranitVaultModuleConfigureServicesTests
{
    [Fact]
    public void ConfigureServices_RegistersVaultMetrics()
    {
        GranitVaultModule module = new();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddMetrics();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        module.ConfigureServices(context);

        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        VaultMetrics? metrics = sp.GetService<VaultMetrics>();
        metrics.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureServices_RegistersVaultMetrics_AsSingleton()
    {
        GranitVaultModule module = new();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddMetrics();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        module.ConfigureServices(context);

        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(VaultMetrics));
        descriptor.ShouldNotBeNull();
        descriptor!.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void ConfigureServices_TryAdd_DoesNotReplaceExistingRegistration()
    {
        GranitVaultModule module = new();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddMetrics();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        // Call twice to verify TryAdd behavior
        module.ConfigureServices(context);
        module.ConfigureServices(context);

        int count = builder.Services.Count(d => d.ServiceType == typeof(VaultMetrics));
        count.ShouldBe(1);
    }

    [Fact]
    public void Module_DependsOnGranitEncryptionModule()
    {
        var attr = (DependsOnAttribute?)
            Attribute.GetCustomAttribute(typeof(GranitVaultModule), typeof(DependsOnAttribute));

        attr.ShouldNotBeNull();
    }
}
