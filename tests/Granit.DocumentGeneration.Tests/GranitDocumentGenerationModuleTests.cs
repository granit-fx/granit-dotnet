using Granit.DocumentGeneration.Pipeline;
using Granit.Modularity;
using Granit.Templating;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DocumentGeneration.Tests;

public sealed class GranitDocumentGenerationModuleTests
{
    [Fact]
    public void ConfigureServices_Registers_IDocumentGenerator()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();
        IHostApplicationBuilder builder = Substitute.For<IHostApplicationBuilder>();
        builder.Services.Returns(services);

        GranitDocumentGenerationModule module = new();
        ServiceConfigurationContext context = new(services, configuration, builder);
        module.ConfigureServices(context);

        services.ShouldContain(d =>
            d.ServiceType == typeof(IDocumentGenerator) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void Module_DependsOn_GranitTemplatingModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitDocumentGenerationModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attributes.Length.ShouldBe(1);
        attributes[0].DependedTypes.ShouldContain(typeof(GranitTemplatingModule));
    }

    [Fact]
    public void Module_IsSealed() => typeof(GranitDocumentGenerationModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsGranitModule()
    {
        GranitDocumentGenerationModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }
}
