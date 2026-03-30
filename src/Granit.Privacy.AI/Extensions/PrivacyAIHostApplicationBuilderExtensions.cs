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
    /// from the <c>AI:Privacy</c> configuration section.
    /// </summary>
    /// <remarks>
    /// Registers <see cref="IAIPiiDetector"/> backed by an LLM via <see cref="AI.IAIChatClientFactory"/>.
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

        if (builder.Environment.IsProduction())
        {
            optionsBuilder.Validate(
                opts => opts.FailMode != PiiDetectionFailMode.Open,
                "AI:Privacy:FailMode 'Open' is forbidden in production — "
                + "PII may go undetected, violating GDPR Art. 25 (Privacy by Design). "
                + "Use 'Closed' instead.");
        }

        builder.Services.TryAddScoped<IAIPiiDetector, LlmPiiDetector>();

        return builder;
    }
}
