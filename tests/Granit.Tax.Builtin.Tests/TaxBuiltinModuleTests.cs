using Granit.Invoicing;
using Granit.Modularity;
using Granit.Tax;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Tax.Builtin.Tests;

public sealed class TaxBuiltinModuleTests
{
    [Fact]
    public void ConfigureServices_RegistersTaxServices()
    {
        // Arrange
        GranitTaxBuiltinModule module = new();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder);

        // Act
        module.ConfigureServices(context);

        // Assert — verify registrations without resolving transitive dependencies
        IServiceCollection services = builder.Services;
        services.ShouldContain(d => d.ServiceType == typeof(ITaxCalculator));
        services.ShouldContain(d => d.ServiceType == typeof(ITaxIdValidator));
        services.ShouldContain(d => d.ServiceType == typeof(ITaxRateProvider));
    }
}
