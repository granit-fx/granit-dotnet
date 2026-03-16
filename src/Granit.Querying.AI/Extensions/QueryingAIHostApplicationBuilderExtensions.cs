using System.Diagnostics.CodeAnalysis;
using Granit.Querying.AI.Internal;
using Granit.Querying.AI.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Querying.AI.Extensions;

/// <summary>
/// Extension methods for registering Granit.Querying.AI services.
/// </summary>
// DI wiring only — no logic to unit test.
[ExcludeFromCodeCoverage]
public static class QueryingAIHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the AI-powered natural language query translator to the application.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitQueryingAI(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<QueryingAIOptions>()
            .BindConfiguration(QueryingAIOptions.SectionName);

        builder.Services.TryAddSingleton<INaturalLanguageQueryTranslator, LlmNaturalLanguageQueryTranslator>();

        return builder;
    }
}
