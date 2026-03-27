using System.Diagnostics.CodeAnalysis;
using Granit.QueryEngine.AI.Internal;
using Granit.QueryEngine.AI.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.QueryEngine.AI.Extensions;

/// <summary>
/// Extension methods for registering Granit.QueryEngine.AI services.
/// </summary>
// DI wiring only — no logic to unit test.
[ExcludeFromCodeCoverage]
public static class QueryEngineAIHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the AI-powered natural language query translator to the application.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitQueryEngineAI(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<QueryEngineAIOptions>()
            .BindConfiguration(QueryEngineAIOptions.SectionName);

        builder.Services.TryAddSingleton<IValidateOptions<QueryEngineAIOptions>, QueryEngineAIOptionsValidator>();
        builder.Services.TryAddScoped<INaturalLanguageQueryTranslator, LlmNaturalLanguageQueryTranslator>();

        return builder;
    }
}
