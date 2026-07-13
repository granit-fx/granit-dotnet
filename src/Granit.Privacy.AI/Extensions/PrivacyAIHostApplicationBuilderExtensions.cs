using System.Diagnostics.CodeAnalysis;
using Granit.Privacy.AI.Internal;
using Granit.Privacy.AI.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.Privacy.AI.Extensions;

/// <summary>
/// Extension methods for registering Granit Privacy AI services.
/// </summary>
[ExcludeFromCodeCoverage]
public static class PrivacyAIHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds AI-powered PII detection services and binds <see cref="PrivacyAIOptions"/>
    /// from the <c>Privacy:AI</c> configuration section.
    /// </summary>
    /// <remarks>
    /// Registers <see cref="IAIPiiDetector"/> backed by an LLM via <see cref="Granit.AI.IStructuredCompletion"/> (ADR-064).
    /// Ensure the configured workspace points to a local model (Ollama) or a provider with
    /// a Data Processing Agreement to keep PII within the security perimeter.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitPrivacyAI(this IHostApplicationBuilder builder)
    {
        OptionsBuilder<PrivacyAIOptions> optionsBuilder = builder.Services
            .AddOptions<PrivacyAIOptions>()
            .BindConfiguration(PrivacyAIOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Fail-open silently skips PII redaction whenever the LLM is unavailable —
        // anything mirroring production data (staging, pre-prod, performance,
        // sandbox) must reject it the same way Production does. Only Development
        // is allowed to opt in.
        if (!builder.Environment.IsDevelopment())
        {
            optionsBuilder.Validate(
                opts => opts.FailMode != PiiDetectionFailMode.Open,
                "Privacy:AI:FailMode 'Open' is only permitted in the Development "
                + "environment — outside it, PII would silently go undetected on LLM "
                + "unavailability, violating GDPR Art. 25 (Privacy by Design). "
                + "Use 'Closed' instead.");
        }

        builder.Services.TryAddScoped<IAIPiiDetector, LlmPiiDetector>();

        return builder;
    }
}
