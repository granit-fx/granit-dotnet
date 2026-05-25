using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.TextExtraction.Ocr.AI.Tests;

public sealed class GranitTextExtractionOcrAIModuleTests
{
    [Fact]
    public void Module_inherits_from_GranitModule() =>
        new GranitTextExtractionOcrAIModule().ShouldBeAssignableTo<GranitModule>();

    [Fact]
    public void Module_is_sealed() =>
        typeof(GranitTextExtractionOcrAIModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_DependsOn_GranitTextExtractionModule()
    {
        var attrs = (DependsOnAttribute[])typeof(GranitTextExtractionOcrAIModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: true);

        attrs.SelectMany(a => a.DependedTypes).ShouldContain(typeof(GranitTextExtractionModule));
    }

    [Fact]
    public void Module_does_not_auto_register_services()
    {
        // The module is a marker — the host must opt in via AddAIVisionOcrExtractor.
        // Enabling vision OCR sends document bytes to a third-party LLM provider
        // (GDPR Art. 28 disclosure required), which is why there is no safe default.
        GranitTextExtractionOcrAIModule module = new();
        bool overridesConfigureServices = module.GetType()
            .GetMethod("ConfigureServices")!
            .DeclaringType != typeof(GranitModule);

        overridesConfigureServices.ShouldBeFalse(
            "GranitTextExtractionOcrAIModule must NOT override ConfigureServices — VLM OCR " +
            "is opt-in and requires explicit AddAIVisionOcrExtractor(...) by the host so the " +
            "GDPR Article 28 sub-processor disclosure is a deliberate decision.");
    }
}
