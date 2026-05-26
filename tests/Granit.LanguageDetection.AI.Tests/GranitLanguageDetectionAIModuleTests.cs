using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.LanguageDetection.AI.Tests;

public sealed class GranitLanguageDetectionAIModuleTests
{
    [Fact]
    public void Module_is_sealed_and_inherits_GranitModule()
    {
        typeof(GranitLanguageDetectionAIModule).IsSealed.ShouldBeTrue();
        new GranitLanguageDetectionAIModule().ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_declares_DependsOn_AIExtraction_and_LanguageDetection()
    {
        var attr = (DependsOnAttribute?)Attribute.GetCustomAttribute(
            typeof(GranitLanguageDetectionAIModule), typeof(DependsOnAttribute));

        attr.ShouldNotBeNull();
        attr.DependedTypes.ShouldContain(typeof(Granit.AI.Extraction.GranitAIExtractionModule));
        attr.DependedTypes.ShouldContain(typeof(GranitLanguageDetectionModule));
    }
}
