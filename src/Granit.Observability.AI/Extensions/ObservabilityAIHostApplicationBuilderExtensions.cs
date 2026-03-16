using System.Diagnostics.CodeAnalysis;
using Granit.Observability.AI.Internal;
using Granit.Observability.AI.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Observability.AI.Extensions;

/// <summary>
/// Extension methods for registering AI-powered observability services.
/// </summary>
[ExcludeFromCodeCoverage]
public static class ObservabilityAIHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds AI-powered log analysis and anomaly detection services.
    /// </summary>
    /// <remarks>
    /// Registers <see cref="IAILogAnalyzer"/> backed by an LLM via <c>Granit.AI</c>.
    /// Requires <c>AddGranitAI()</c> to be called first, with at least one AI provider configured.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitObservabilityAI(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<ObservabilityAIOptions>()
            .BindConfiguration(ObservabilityAIOptions.SectionName);

        builder.Services.TryAddSingleton<IAILogAnalyzer, LlmLogAnalyzer>();

        return builder;
    }
}
