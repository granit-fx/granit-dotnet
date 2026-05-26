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
        // Regression for the audit's BREAKING finding #1: previously the composite
        // descriptor was registered as ILanguageDetector via TryAddSingleton and a
        // concrete provider then registered as ILanguageDetector via TryAddEnumerable.
        // The DI container returned the LAST descriptor on a non-IEnumerable resolve,
        // silently bypassing the composite. The fix splits the provider marker
        // (ILanguageDetectorProvider) from the consumer-facing facade (ILanguageDetector)
        // — this test pins the contract.

        ServiceCollection services = [];
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
        services.AddGranitLanguageDetection();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILanguageDetectorProvider, FakeProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILanguageDetectorProvider, OtherFakeProvider>());

        ServiceProvider sp = services.BuildServiceProvider();
        IEnumerable<ILanguageDetectorProvider> providers = sp.GetServices<ILanguageDetectorProvider>();
        providers.Count().ShouldBe(2);
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
