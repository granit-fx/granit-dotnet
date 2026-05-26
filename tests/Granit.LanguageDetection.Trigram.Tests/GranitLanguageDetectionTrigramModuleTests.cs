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
    public void DI_extension_registers_detector_as_provider_for_the_composite_chain()
    {
        ServiceCollection services = [];
        services.AddGranitLanguageDetectionTrigram();
        ServiceProvider sp = services.BuildServiceProvider();

        // Concrete detectors register under ILanguageDetectorProvider so the composite
        // picks them up via IEnumerable<ILanguageDetectorProvider>. Registering them
        // as ILanguageDetector instead would silently bypass the composite — this test
        // locks the correct marker.
        IEnumerable<ILanguageDetectorProvider> providers = sp.GetServices<ILanguageDetectorProvider>();
        providers.OfType<TrigramLanguageDetector>().Count().ShouldBe(1);
    }

    [Fact]
    public void DI_extension_is_idempotent()
    {
        // TryAddEnumerable + TryAddSingleton guarantee that calling the extension twice
        // does not register the detector twice — both calls converge to a single provider.
        ServiceCollection services = [];
        services.AddGranitLanguageDetectionTrigram();
        services.AddGranitLanguageDetectionTrigram();
        ServiceProvider sp = services.BuildServiceProvider();

        sp.GetServices<ILanguageDetectorProvider>().Count().ShouldBe(1);
    }
}
