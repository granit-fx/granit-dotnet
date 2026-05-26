using Granit.LanguageDetection.Trigram.Extensions;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.LanguageDetection.Trigram.Tests;

public sealed class GranitLanguageDetectionTrigramModuleTests
{
    [Fact]
    public void Module_is_sealed_and_inherits_GranitModule()
    {
        typeof(GranitLanguageDetectionTrigramModule).IsSealed.ShouldBeTrue();
        new GranitLanguageDetectionTrigramModule().ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_declares_DependsOn_GranitLanguageDetectionModule()
    {
        var attr = (DependsOnAttribute?)Attribute.GetCustomAttribute(
            typeof(GranitLanguageDetectionTrigramModule), typeof(DependsOnAttribute));

        attr.ShouldNotBeNull();
        attr.DependedTypes.ShouldContain(typeof(GranitLanguageDetectionModule));
    }

    [Fact]
    public void DI_extension_registers_detector_in_composite_chain()
    {
        ServiceCollection services = [];
        services.AddGranitLanguageDetectionTrigram();
        ServiceProvider sp = services.BuildServiceProvider();

        IEnumerable<ILanguageDetector> all = sp.GetServices<ILanguageDetector>();
        all.OfType<TrigramLanguageDetector>().Count().ShouldBe(1);
    }
}
