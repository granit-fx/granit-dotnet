using Granit.Modularity;
using Granit.Vault.HashiCorp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Vault.HashiCorp.Tests;

public sealed class GranitVaultHashiCorpModuleTests
{
    [Fact]
    public void IsEnabled_InDevelopment_ReturnsFalse()
    {
        GranitVaultHashiCorpModule module = new();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(["--environment", "Development"]);
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        module.IsEnabled(context).ShouldBeFalse();
    }

    [Fact]
    public void ConfigureServices_InProduction_RegistersVaultServices()
    {
        GranitVaultHashiCorpModule module = new();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(["--environment", "Production"]);
        builder.Configuration["Vault:Address"] = "https://vault.test:8200";
        builder.Configuration["Vault:RoleName"] = "test-role";
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        module.ConfigureServices(context);

        ServiceDescriptor? vaultDescriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(VaultClientFactory));
        vaultDescriptor.ShouldNotBeNull();
    }
}
