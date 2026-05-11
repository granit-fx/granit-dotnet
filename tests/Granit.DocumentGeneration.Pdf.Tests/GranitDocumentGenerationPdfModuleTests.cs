using Granit.Browsing;
using Granit.Browsing.Capabilities;
using Granit.DocumentGeneration.Pipeline;
using Granit.Modularity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DocumentGeneration.Pdf.Tests;

public sealed class GranitDocumentGenerationPdfModuleTests
{
    [Fact]
    public void ConfigureServices_Registers_IDocumentRenderer()
    {
        ServiceCollection services = new();
        services.AddLogging();
        IConfiguration configuration = new ConfigurationBuilder().Build();
        services.AddSingleton(configuration);
        // Browsing provider stub — required by AddGranitDocumentGenerationPdf's fail-fast.
        services.AddSingleton(Substitute.For<IHeadlessBrowser>());
        services.AddSingleton(Substitute.For<IPdfCapability>());
        IHostApplicationBuilder builder = Substitute.For<IHostApplicationBuilder>();
        builder.Services.Returns(services);

        GranitDocumentGenerationPdfModule module = new();
        ServiceConfigurationContext context = new(services, configuration, builder);
        module.ConfigureServices(context);

        ServiceProvider provider = services.BuildServiceProvider();
        IDocumentRenderer? renderer = provider.GetService<IDocumentRenderer>();
        renderer.ShouldNotBeNull();
    }

    [Fact]
    public void Module_DependsOn_Browsing_and_DocumentGeneration()
    {
        DependsOnAttribute[] attributes = typeof(GranitDocumentGenerationPdfModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attributes.Length.ShouldBe(1);
        attributes[0].DependedTypes.ShouldContain(typeof(GranitBrowsingModule));
        attributes[0].DependedTypes.ShouldContain(typeof(GranitDocumentGenerationModule));
    }
}
