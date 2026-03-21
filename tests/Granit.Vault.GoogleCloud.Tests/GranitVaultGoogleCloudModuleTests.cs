using Granit.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Vault.GoogleCloud.Tests;

public sealed class GranitVaultGoogleCloudModuleTests
{
    [Fact]
    public void IsEnabled_InDevelopment_ReturnsFalse()
    {
        GranitVaultGoogleCloudModule module = new();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(["--environment", "Development"]);
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        module.IsEnabled(context).ShouldBeFalse();
    }

    [Fact]
    public void IsEnabled_InProduction_ReturnsTrue()
    {
        GranitVaultGoogleCloudModule module = new();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(["--environment", "Production"]);
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        module.IsEnabled(context).ShouldBeTrue();
    }

    [Fact]
    public void IsEnabled_InStaging_ReturnsTrue()
    {
        GranitVaultGoogleCloudModule module = new();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(["--environment", "Staging"]);
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        module.IsEnabled(context).ShouldBeTrue();
    }

    [Fact]
    public void Module_DependsOnGranitVaultModule()
    {
        var attr = (DependsOnAttribute?)
            Attribute.GetCustomAttribute(typeof(GranitVaultGoogleCloudModule), typeof(DependsOnAttribute));

        attr.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureServices_RegistersExpectedServices()
    {
        GranitVaultGoogleCloudModule module = new();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(["--environment", "Production"]);
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        module.ConfigureServices(context);

        builder.Services.ShouldContain(d => d.ServiceType == typeof(ITransitEncryptionService));
    }
}
