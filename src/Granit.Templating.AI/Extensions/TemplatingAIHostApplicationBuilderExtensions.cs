using System.Diagnostics.CodeAnalysis;
using Granit.Templating.AI.Internal;
using Granit.Templating.AI.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.Templating.AI.Extensions;

/// <summary>
/// Extension methods for registering AI-powered template assistance services.
/// </summary>
[ExcludeFromCodeCoverage]
public static class TemplatingAIHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the AI-powered template assistant for Scriban template generation and data enrichment.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers <see cref="IAITemplateAssistant"/> backed by an LLM via <c>IAIChatClientFactory</c>.
    /// </para>
    /// <para>
    /// Requires <c>Granit.AI</c> to be configured with at least one provider and a workspace
    /// matching <see cref="TemplatingAIOptions.WorkspaceName"/>.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitTemplatingAI(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<TemplatingAIOptions>()
            .BindConfiguration(TemplatingAIOptions.SectionName);

        builder.Services.TryAddSingleton<IValidateOptions<TemplatingAIOptions>, TemplatingAIOptionsValidator>();
        builder.Services.TryAddSingleton<IAITemplateAssistant, LlmTemplateAssistant>();

        return builder;
    }
}
