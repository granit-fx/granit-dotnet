using System;
using Granit.Browsing;
using Granit.Browsing.Capabilities;
using Granit.DocumentGeneration.Pdf.Extensions;
using Granit.DocumentGeneration.Pdf.Options;
using Granit.DocumentGeneration.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DocumentGeneration.Pdf.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitDocumentGenerationPdf_WithBrowsingProvider_RegistersRenderer()
    {
        ServiceCollection services = CreateServicesWithBrowsing();

        services.AddGranitDocumentGenerationPdf();

        ServiceProvider provider = services.BuildServiceProvider();
        IDocumentRenderer? renderer = provider.GetService<IDocumentRenderer>();
        renderer.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitDocumentGenerationPdf_WithoutBrowsingProvider_ThrowsAtRegistration()
    {
        ServiceCollection services = CreateServices();

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            services.AddGranitDocumentGenerationPdf());

        ex.Message.ShouldContain("Granit.Browsing");
        ex.Message.ShouldContain("AddGranitBrowsingPuppeteerSharp");
        ex.Message.ShouldContain("AddGranitBrowsingPlaywright");
    }

    [Fact]
    public void AddGranitDocumentGenerationPdf_BindsPdfRenderOptions()
    {
        ServiceCollection services = CreateServicesWithBrowsing();
        services.AddGranitDocumentGenerationPdf();

        ServiceProvider provider = services.BuildServiceProvider();
        IOptions<PdfRenderOptions> opts = provider.GetRequiredService<IOptions<PdfRenderOptions>>();
        opts.Value.PaperFormat.ShouldBe("A4");
    }

    private static ServiceCollection CreateServices()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        return services;
    }

    private static ServiceCollection CreateServicesWithBrowsing()
    {
        ServiceCollection services = CreateServices();
        services.AddSingleton(Substitute.For<IHeadlessBrowser>());
        services.AddSingleton(Substitute.For<IPdfCapability>());
        return services;
    }
}
