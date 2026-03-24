using Granit.Modularity;
using Granit.Templating;
using Granit.Templating.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DocumentGeneration.Excel.Tests;

public sealed class GranitDocumentGenerationExcelModuleTests
{
    [Fact]
    public void ConfigureServices_Registers_ITemplateEngine()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();
        IHostApplicationBuilder builder = Substitute.For<IHostApplicationBuilder>();
        builder.Services.Returns(services);

        GranitDocumentGenerationExcelModule module = new();
        ServiceConfigurationContext context = new(services, configuration, builder);
        module.ConfigureServices(context);

        services.ShouldContain(d =>
            d.ServiceType == typeof(ITemplateEngine) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void Module_DependsOn_GranitTemplatingModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitDocumentGenerationExcelModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attributes.Length.ShouldBe(1);
        attributes[0].DependedTypes.ShouldContain(typeof(GranitTemplatingModule));
    }

    [Fact]
    public void Module_IsSealed() => typeof(GranitDocumentGenerationExcelModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsGranitModule()
    {
        GranitDocumentGenerationExcelModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }
}
