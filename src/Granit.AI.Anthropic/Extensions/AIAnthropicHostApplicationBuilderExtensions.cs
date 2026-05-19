using System.Diagnostics.CodeAnalysis;
using Granit.AI.Anthropic.Diagnostics;
using Granit.AI.Anthropic.Internal;
using Granit.AI.Anthropic.Options;
using Granit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.AI.Anthropic.Extensions;

/// <summary>
/// Extension methods for registering the Anthropic AI provider.
/// </summary>
[ExcludeFromCodeCoverage]
public static class AIAnthropicHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Anthropic (Claude) AI provider to the application.
    /// </summary>
    /// <remarks>
    /// Binds <see cref="AnthropicProviderOptions"/> from the <c>AI:Anthropic</c> configuration section,
    /// registers options validation, registers the named <see cref="HttpClient"/> consumed by the
    /// Anthropic SDK (timeout handling is owned by the SDK itself), and registers
    /// <see cref="AnthropicProviderFactory"/> as an <see cref="IAIProviderFactory"/> implementation.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitAIAnthropic(this IHostApplicationBuilder builder)
    {
        GranitActivitySourceRegistry.Register(AIAnthropicActivitySource.Name);

        builder.Services
            .AddOptions<AnthropicProviderOptions>()
            .BindConfiguration(AnthropicProviderOptions.SectionName)
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<AnthropicProviderOptions>, AnthropicProviderOptionsValidator>();

        // The Anthropic SDK enforces its own per-call Timeout; setting HttpClient.Timeout to
        // InfiniteTimeSpan prevents the inner HttpClient timeout from racing the SDK cancellation handler.
        builder.Services
            .AddHttpClient(AnthropicProviderFactory.HttpClientName, client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
            });

        builder.Services.AddSingleton<IAIProviderFactory, AnthropicProviderFactory>();

        return builder;
    }
}
