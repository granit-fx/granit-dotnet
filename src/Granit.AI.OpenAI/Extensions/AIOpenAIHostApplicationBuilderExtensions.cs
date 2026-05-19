using System.Diagnostics.CodeAnalysis;
using Granit.AI.OpenAI.Diagnostics;
using Granit.AI.OpenAI.Internal;
using Granit.AI.OpenAI.Options;
using Granit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.AI.OpenAI.Extensions;

/// <summary>
/// Extension methods for registering the OpenAI AI provider.
/// </summary>
// DI wiring only — no logic to unit test.
[ExcludeFromCodeCoverage]
public static class AIOpenAIHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds <c>Granit.AI.OpenAI</c> services: <see cref="IAIProviderFactory"/> backed by the OpenAI API.
    /// </summary>
    /// <remarks>
    /// Reads <see cref="OpenAIProviderOptions"/> from the <c>AI:OpenAI</c> configuration section.
    /// The API key should be injected from <c>Granit.Vault</c>; never hardcode it in appsettings.
    /// Registers the named <see cref="HttpClient"/> consumed by the OpenAI SDK (timeout handling
    /// is owned by the SDK itself via <see cref="OpenAIProviderOptions.Timeout"/>).
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitAIOpenAI(this IHostApplicationBuilder builder)
    {
        GranitActivitySourceRegistry.Register(AIOpenAIActivitySource.Name);

        builder.Services
            .AddOptions<OpenAIProviderOptions>()
            .BindConfiguration(OpenAIProviderOptions.SectionName)
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<OpenAIProviderOptions>, OpenAIProviderOptionsValidator>();

        // TimeProvider may already be registered by the host; TryAdd avoids overriding it.
        builder.Services.TryAddSingleton(TimeProvider.System);

        // The OpenAI SDK enforces its own NetworkTimeout; setting HttpClient.Timeout to
        // InfiniteTimeSpan prevents the inner HttpClient timeout from racing the SDK cancellation handler.
        builder.Services
            .AddHttpClient(OpenAIProviderFactory.HttpClientName, client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
            });

        builder.Services.AddSingleton<IAIProviderFactory, OpenAIProviderFactory>();

        return builder;
    }
}
