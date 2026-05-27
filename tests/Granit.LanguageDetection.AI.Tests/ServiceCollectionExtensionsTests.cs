using Granit.AI.Extraction.Redaction;
using Granit.LanguageDetection.AI.Extensions;
using Granit.LanguageDetection.AI.Internal;
using Granit.LanguageDetection.AI.Options;
using Granit.LanguageDetection.AI.Prompts;
using Granit.LanguageDetection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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

    [Theory]
    [InlineData("LanguageDetection:AI:MaxContentLength", "0")]
    [InlineData("LanguageDetection:AI:MaxContentLength", "-1")]
    [InlineData("LanguageDetection:AI:MaxAICallsPerHourPerTenant", "0")]
    [InlineData("LanguageDetection:AI:MaxAICallsPerHourPerTenant", "-100")]
    [InlineData("LanguageDetection:AI:TimeoutSeconds", "0")]
    [InlineData("LanguageDetection:AI:TimeoutSeconds", "-5")]
    [InlineData("LanguageDetection:AI:TimeoutSeconds", "999")]
    [InlineData("LanguageDetection:AI:WorkspaceName", "")]
    public void AddGranitLanguageDetectionAI_rejects_out_of_range_options_at_resolution(string key, string value)
    {
        // A silently-degraded configuration would inflate the injection_attempt counter
        // with false positives — for example MaxContentLength=0 yields an empty sample
        // and every call resolves to a schema reject. We must surface the misconfiguration
        // loudly. ValidateOnStart triggers via IStartupValidator on full host build, but
        // even when only the options pipeline is exercised, validation fires on first
        // Get().
        ServiceCollection services = BuildBaseServices(new Dictionary<string, string?>
        {
            [key] = value,
        });
        services.AddGranitLanguageDetectionAI();
        ServiceProvider sp = services.BuildServiceProvider();

        Action act = () => _ = sp.GetRequiredService<IOptions<LanguageDetectionAIOptions>>().Value;

        act.ShouldThrow<OptionsValidationException>();
    }

    [Fact]
    public void AddGranitLanguageDetectionAI_accepts_default_options_under_validation()
    {
        // Sanity check: shipped defaults must pass their own validators. A miscalibrated
        // [Range] attribute would crash every host that takes the package as-is.
        ServiceCollection services = BuildBaseServices();
        services.AddGranitLanguageDetectionAI();
        ServiceProvider sp = services.BuildServiceProvider();

        LanguageDetectionAIOptions options =
            sp.GetRequiredService<IOptions<LanguageDetectionAIOptions>>().Value;

        options.WorkspaceName.ShouldBe("default");
        options.MaxAICallsPerHourPerTenant.ShouldBe(1_000);
        options.MaxContentLength.ShouldBe(2_048);
        options.TimeoutSeconds.ShouldBe(10);
        options.RedactPIIBeforeLLMCall.ShouldBeTrue();
    }

    [Fact]
    public void AddGranitLanguageDetectionAI_registers_redaction_startup_check()
    {
        // The startup probe is the user-visible signal that the NoOpAIContentRedactor
        // identity default is leaving PII unredacted while RedactPIIBeforeLLMCall=true.
        // It is registered via the shared Granit.AI.Extraction probe (factory-based, so
        // ImplementationType is null) — assert by resolving the hosted-service set.
        ServiceCollection services = BuildBaseServices();
        services.AddGranitLanguageDetectionAI();
        ServiceProvider sp = services.BuildServiceProvider();

        sp.GetServices<IHostedService>()
            .Any(h => h.GetType().Name == "AIRedactionStartupCheck")
            .ShouldBeTrue();
    }

    private static ServiceCollection BuildBaseServices(IDictionary<string, string?>? overrides = null)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(overrides ?? new Dictionary<string, string?>())
            .Build();
        ServiceCollection services = [];
        services.AddSingleton(configuration);
        services.AddOptions();
        services.AddLogging();
        services.AddMetrics();
        services.AddGranitLanguageDetection();
        // GranitLanguageDetectionAIModule DependsOn GranitAIExtractionModule, which
        // registers the redactor seam consumed by the shared startup probe. Mirror that
        // here since the unit test calls AddGranitLanguageDetectionAI() directly.
        services.AddSingleton<IAIContentRedactor, NoOpAIContentRedactor>();
        return services;
    }
}
