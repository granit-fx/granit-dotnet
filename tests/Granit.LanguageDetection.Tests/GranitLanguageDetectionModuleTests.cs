using Granit.LanguageDetection.Extensions;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Xunit;

namespace Granit.LanguageDetection.Tests;

public sealed class GranitLanguageDetectionModuleTests
{
    [Fact]
    public void Module_is_sealed_and_inherits_GranitModule()
    {
        typeof(GranitLanguageDetectionModule).IsSealed.ShouldBeTrue();
        new GranitLanguageDetectionModule().ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_declares_no_DependsOn()
    {
        // The base detection module is a horizontal cross-cutting concern with zero
        // declared dependencies (Granit is the implicit base). Concrete provider
        // packages (.Trigram, future .AI.*) depend on this one, never the reverse.
        object[] attrs = typeof(GranitLanguageDetectionModule).GetCustomAttributes(typeof(DependsOnAttribute), inherit: true);
        attrs.ShouldBeEmpty();
    }

    [Fact]
    public void DI_resolves_ILanguageDetector_to_the_composite_even_when_providers_are_registered()
    {
        // Pins the marker-split contract: providers register under
        // ILanguageDetectorProvider (fanned into IEnumerable<>), the composite is the
        // sole ILanguageDetector. Without the split, a concrete provider registered
        // via TryAddEnumerable<ILanguageDetector> would become the last descriptor for
        // ILanguageDetector and the DI container's non-IEnumerable resolve would
        // return it instead of the composite, silently bypassing the priority chain.

        ServiceCollection services = [];
        services.AddMetrics();
        services.AddGranitLanguageDetection();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILanguageDetectorProvider, FakeProvider>());

        ServiceProvider sp = services.BuildServiceProvider();
        ILanguageDetector resolved = sp.GetRequiredService<ILanguageDetector>();

        resolved.ShouldBeOfType<CompositeLanguageDetector>();
    }

    [Fact]
    public void DI_aggregates_every_registered_provider_into_the_composite()
    {
        ServiceCollection services = [];
        services.AddMetrics();
        services.AddGranitLanguageDetection();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILanguageDetectorProvider, FakeProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILanguageDetectorProvider, OtherFakeProvider>());

        ServiceProvider sp = services.BuildServiceProvider();
        IEnumerable<ILanguageDetectorProvider> providers = sp.GetServices<ILanguageDetectorProvider>();
        providers.Count().ShouldBe(2);
    }

    [Fact]
    public void AddGranitLanguageDetection_strips_any_prior_ILanguageDetector_binding()
    {
        // A stray services.AddSingleton<ILanguageDetector, MyDetector>() registered
        // BEFORE AddGranitLanguageDetection() used to slip past TryAddSingleton and
        // silently replace the composite (last-wins in the DI container). The fix
        // strips any pre-existing ILanguageDetector descriptor so the composite always
        // wins; custom detectors must register under ILanguageDetectorProvider.

        ServiceCollection services = [];
        services.AddMetrics();
        services.AddSingleton<ILanguageDetector, FakeProvider>(); // misregistered detector
        services.AddGranitLanguageDetection();

        ServiceProvider sp = services.BuildServiceProvider();
        ILanguageDetector resolved = sp.GetRequiredService<ILanguageDetector>();

        resolved.ShouldBeOfType<CompositeLanguageDetector>();
    }

    private sealed class FakeProvider : ILanguageDetectorProvider
    {
        public int Priority => 100;
        public Task<string?> DetectAsync(string content, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>("en");
    }

    private sealed class OtherFakeProvider : ILanguageDetectorProvider
    {
        public int Priority => 50;
        public Task<string?> DetectAsync(string content, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>("fr");
    }
}
