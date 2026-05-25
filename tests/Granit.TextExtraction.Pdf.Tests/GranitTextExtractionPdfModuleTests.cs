using System.Diagnostics.Metrics;
using Granit.Modularity;
using Granit.TextExtraction.Pdf.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using ExtractionOptions = Granit.TextExtraction.Options.GranitTextExtractionOptions;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Granit.TextExtraction.Pdf.Tests;

public sealed class GranitTextExtractionPdfModuleTests
{
    [Fact]
    public void Module_inherits_from_GranitModule() =>
        new GranitTextExtractionPdfModule().ShouldBeAssignableTo<GranitModule>();

    [Fact]
    public void Module_is_sealed() =>
        typeof(GranitTextExtractionPdfModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_DependsOn_GranitTextExtractionModule()
    {
        var attrs = (DependsOnAttribute[])typeof(GranitTextExtractionPdfModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: true);

        attrs.SelectMany(a => a.DependedTypes).ShouldContain(typeof(GranitTextExtractionModule));
    }

    [Fact]
    public async Task Pipeline_routes_application_pdf_after_AddGranitTextExtractionPdf()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IMeterFactory, NoopMeterFactory>();
        services.AddSingleton(MEOptions.Create(new ExtractionOptions()));
        services.AddLogging();
        services.AddGranitTextExtractionPdf();

        using ServiceProvider sp = services.BuildServiceProvider();
        ITextExtractionPipeline pipeline = sp.GetRequiredService<ITextExtractionPipeline>();

        byte[] pdf = PdfFixtures.TextOnly("DI smoke");
        using MemoryStream stream = new(pdf);

        TextExtractionResult result =
            await pipeline.ExtractAsync(stream, "application/pdf", TestContext.Current.CancellationToken);

        result.ExtractorName.ShouldBe(PdfTextExtractor.ExtractorName);
        result.Content.ShouldContain("DI smoke");
    }

    private sealed class NoopMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];

        public Meter Create(MeterOptions options)
        {
            Meter meter = new(options);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (Meter meter in _meters)
            {
                meter.Dispose();
            }

            _meters.Clear();
        }
    }
}
