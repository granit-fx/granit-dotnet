using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.TextExtraction.Ocr.Tesseract.Tests;

public sealed class GranitTextExtractionOcrTesseractModuleTests
{
    [Fact]
    public void Module_inherits_from_GranitModule() =>
        new GranitTextExtractionOcrTesseractModule().ShouldBeAssignableTo<GranitModule>();

    [Fact]
    public void Module_is_sealed() =>
        typeof(GranitTextExtractionOcrTesseractModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_DependsOn_GranitTextExtractionModule()
    {
        var attrs = (DependsOnAttribute[])typeof(GranitTextExtractionOcrTesseractModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: true);

        attrs.SelectMany(a => a.DependedTypes).ShouldContain(typeof(GranitTextExtractionModule));
    }

    [Fact]
    public void Module_does_not_auto_register_services()
    {
        // The module is a marker — the host must opt in via AddTesseractOcrExtractor.
        // Enabling Tesseract requires native libtesseract + traineddata on disk, so
        // there's no safe default the framework can wire automatically.
        GranitTextExtractionOcrTesseractModule module = new();
        bool overridesConfigureServices = module.GetType()
            .GetMethod("ConfigureServices")!
            .DeclaringType != typeof(GranitModule);

        overridesConfigureServices.ShouldBeFalse(
            "GranitTextExtractionOcrTesseractModule must NOT override ConfigureServices — " +
            "Tesseract requires native libraries + traineddata files on disk, so wiring " +
            "is opt-in via AddTesseractOcrExtractor(...).");
    }
}
