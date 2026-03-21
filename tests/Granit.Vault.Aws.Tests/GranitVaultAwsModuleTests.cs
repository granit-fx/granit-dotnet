using Granit.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Vault.Aws.Tests;

public sealed class GranitVaultAwsModuleTests
{
    [Fact]
    public void IsEnabled_InDevelopment_ReturnsFalse()
    {
        GranitVaultAwsModule module = new();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(["--environment", "Development"]);
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        module.IsEnabled(context).ShouldBeFalse();
    }

    [Fact]
    public void IsEnabled_InProduction_ReturnsTrue()
    {
        GranitVaultAwsModule module = new();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(["--environment", "Production"]);
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        module.IsEnabled(context).ShouldBeTrue();
    }

    [Fact]
    public void IsEnabled_InStaging_ReturnsTrue()
    {
        GranitVaultAwsModule module = new();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(["--environment", "Staging"]);
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        module.IsEnabled(context).ShouldBeTrue();
    }

    [Fact]
    public void Module_DependsOnGranitVaultModule()
    {
        var attr = (DependsOnAttribute?)
            Attribute.GetCustomAttribute(typeof(GranitVaultAwsModule), typeof(DependsOnAttribute));

        attr.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureServices_RegistersExpectedServices()
    {
        GranitVaultAwsModule module = new();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(["--environment", "Production"]);
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        module.ConfigureServices(context);

        builder.Services.ShouldContain(d => d.ServiceType == typeof(ITransitEncryptionService));
    }
}
