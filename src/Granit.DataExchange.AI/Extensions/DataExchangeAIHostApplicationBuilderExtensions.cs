using System.Diagnostics.CodeAnalysis;
using Granit.DataExchange.AI.Internal;
using Granit.DataExchange.AI.Options;
using Granit.DataExchange.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.DataExchange.AI.Extensions;

/// <summary>
/// Extension methods for registering AI-powered semantic mapping services.
/// </summary>
[ExcludeFromCodeCoverage]
public static class DataExchangeAIHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the AI-powered semantic mapping service for data exchange.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Replaces the default <c>NullSemanticMappingService</c> with <see cref="AISemanticMappingService"/>
    /// which uses an LLM via <c>IAIChatClientFactory</c> to suggest column-to-property mappings.
    /// </para>
    /// <para>
    /// Requires <c>Granit.AI</c> to be configured with at least one provider and a workspace
    /// matching <see cref="DataExchangeAIOptions.WorkspaceName"/>.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitDataExchangeAI(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<DataExchangeAIOptions>()
            .BindConfiguration(DataExchangeAIOptions.SectionName)
            .ValidateOnStart();

        builder.Services.TryAddSingleton<IValidateOptions<DataExchangeAIOptions>, DataExchangeAIOptionsValidator>();
        builder.Services.AddSemanticMappingService<AISemanticMappingService>();

        return builder;
    }
}
