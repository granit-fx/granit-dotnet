using Granit.Modularity;
using Granit.TextExtraction.Pdf.Extensions;
using Granit.TextExtraction.Pdf.Ocr.Extensions;
using Granit.TextExtraction.Pdf.Ocr.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.TextExtraction.Pdf.Ocr.Tests;

public sealed class GranitTextExtractionPdfOcrModuleTests
{
    [Fact]
    public void Module_DependsOn_TextExtraction_and_Pdf_base()
    {
        // Granit's DependsOnAttribute is the modularity contract — assert via reflection so a
        // regression in the [DependsOn] list lights up here instead of at host-build time.
        DependsOnAttribute attr = typeof(GranitTextExtractionPdfOcrModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .Single();

        attr.DependedTypes.ShouldContain(typeof(GranitTextExtractionModule));
        attr.DependedTypes.ShouldContain(typeof(Granit.TextExtraction.Pdf.GranitTextExtractionPdfModule));
    }

    [Fact]
    public void AddGranitTextExtractionPdfOcr_replaces_PdfTextExtractor_with_PdfOcrTextExtractor()
    {
        ServiceCollection services = [];
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddGranitTextExtractionPdf();
        services.AddGranitTextExtractionPdfOcr();

        ServiceProvider sp = services.BuildServiceProvider();
        IEnumerable<ITextExtractor> extractors = sp.GetServices<ITextExtractor>();

        extractors.OfType<PdfOcrTextExtractor>().Count().ShouldBe(1);
        extractors.OfType<Granit.TextExtraction.Pdf.PdfTextExtractor>().ShouldBeEmpty(
            "the OCR-aware extractor must REPLACE the text-only one — registering both " +
            "would have two extractors fighting over application/pdf.");
    }

    [Fact]
    public void AddGranitTextExtractionPdfOcr_honours_configure_callback()
    {
        ServiceCollection services = [];
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddGranitTextExtractionPdfOcr(o =>
        {
            o.MinNativeCharsPerPage = 7;
            o.RenderDpi = 150;
        });

        ServiceProvider sp = services.BuildServiceProvider();
        Microsoft.Extensions.Options.IOptions<PdfOcrOptions> opts =
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PdfOcrOptions>>();

        opts.Value.MinNativeCharsPerPage.ShouldBe(7);
        opts.Value.RenderDpi.ShouldBe(150);
    }
}
