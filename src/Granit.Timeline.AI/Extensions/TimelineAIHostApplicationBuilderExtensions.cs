using System.Diagnostics.CodeAnalysis;
using Granit.Diagnostics;
using Granit.Timeline.AI.Diagnostics;
using Granit.Timeline.AI.Internal;
using Granit.Timeline.AI.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Timeline.AI.Extensions;

/// <summary>
/// Extension methods for registering AI-powered timeline analysis services.
/// </summary>
// DI wiring only — no logic to unit test.
[ExcludeFromCodeCoverage]
public static class TimelineAIHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds <c>Granit.Timeline.AI</c> services: AI-powered timeline summarization
    /// and anomaly detection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reads <see cref="TimelineAIOptions"/> from the <c>"AI:Timeline"</c> configuration section.
    /// Requires <c>Granit.AI</c> core services (<c>AddGranitAI()</c>) and at least one AI provider
    /// to be registered beforehand.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitTimelineAI(
        this IHostApplicationBuilder builder)
    {
        GranitActivitySourceRegistry.Register(TimelineAIActivitySource.Name);

        builder.Services
            .AddOptions<TimelineAIOptions>()
            .BindConfiguration(TimelineAIOptions.SectionName);

        builder.Services.TryAddSingleton<TimelineAIMetrics>();
        builder.Services.TryAddScoped<ITimelineSummarizer, LlmTimelineSummarizer>();
        builder.Services.TryAddScoped<ITimelineAnomalyDetector, LlmTimelineAnomalyDetector>();

        return builder;
    }
}
