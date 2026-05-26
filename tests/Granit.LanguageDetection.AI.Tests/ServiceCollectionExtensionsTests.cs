using Granit.LanguageDetection.AI.Extensions;
using Granit.LanguageDetection.AI.Internal;
using Granit.LanguageDetection.AI.Prompts;
using Granit.LanguageDetection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.LanguageDetection.AI.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitLanguageDetectionAI_registers_AILanguageDetector_as_provider_not_facade()
    {
        // Concrete detectors register under ILanguageDetectorProvider so the composite
        // picks them up via IEnumerable<>. Registering as ILanguageDetector directly
        // would silently replace the composite — locked by an architecture invariant.
        ServiceCollection services = BuildBaseServices();

        services.AddGranitLanguageDetectionAI();

        ServiceDescriptor[] providerDescriptors =
        [.. services.Where(d => d.ServiceType == typeof(ILanguageDetectorProvider)
                                && d.ImplementationType == typeof(AILanguageDetector))];

        providerDescriptors.Length.ShouldBe(1);

        // And NOT registered directly under ILanguageDetector — composite stays the sole facade.
        services.Where(d => d.ServiceType == typeof(ILanguageDetector)
                            && d.ImplementationType == typeof(AILanguageDetector))
            .ShouldBeEmpty();
    }

    [Fact]
    public void AddGranitLanguageDetectionAI_is_idempotent()
    {
        // TryAddEnumerable on ILanguageDetectorProvider + TryAddSingleton on metrics +
        // prompt builder means double-registration converges to a single descriptor.
        ServiceCollection services = BuildBaseServices();

        services.AddGranitLanguageDetectionAI();
        services.AddGranitLanguageDetectionAI();

        services.Count(d => d.ServiceType == typeof(ILanguageDetectorProvider)
                            && d.ImplementationType == typeof(AILanguageDetector))
            .ShouldBe(1);
    }

    [Fact]
    public void AddGranitLanguageDetectionAI_registers_default_prompt_builder()
    {
        ServiceCollection services = BuildBaseServices();

        services.AddGranitLanguageDetectionAI();
        ServiceProvider sp = services.BuildServiceProvider();

        sp.GetRequiredService<IAILanguageDetectionPromptBuilder>()
            .ShouldBeOfType<DefaultAILanguageDetectionPromptBuilder>();
    }

    private static ServiceCollection BuildBaseServices()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = [];
        services.AddSingleton(configuration);
        services.AddOptions();
        services.AddLogging();
        services.AddMetrics();
        services.AddGranitLanguageDetection();
        return services;
    }
}
