using System.Diagnostics.CodeAnalysis;
using Granit.AI.AzureOpenAI.Diagnostics;
using Granit.AI.AzureOpenAI.Internal;
using Granit.AI.AzureOpenAI.Options;
using Granit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.AI.AzureOpenAI.Extensions;

/// <summary>
/// Extension methods for registering the Azure OpenAI AI provider.
/// </summary>
[ExcludeFromCodeCoverage]
public static class AIAzureOpenAIHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds <c>Granit.AI.AzureOpenAI</c> services.
    /// </summary>
    /// <remarks>
    /// Reads <see cref="AzureOpenAIProviderOptions"/> from the <c>AI:AzureOpenAI</c> configuration section.
    /// When <see cref="AzureOpenAIProviderOptions.ApiKey"/> is empty, the provider uses
    /// <c>DefaultAzureCredential</c> (Managed Identity) — recommended for production.
    /// Registers the named <see cref="HttpClient"/> consumed by the Azure OpenAI SDK.
    /// </remarks>
    public static IHostApplicationBuilder AddGranitAIAzureOpenAI(this IHostApplicationBuilder builder)
    {
        GranitActivitySourceRegistry.Register(AIAzureOpenAIActivitySource.Name);

        builder.Services
            .AddOptions<AzureOpenAIProviderOptions>()
            .BindConfiguration(AzureOpenAIProviderOptions.SectionName)
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<AzureOpenAIProviderOptions>, AzureOpenAIProviderOptionsValidator>();

        // The Azure OpenAI SDK enforces its own NetworkTimeout; setting HttpClient.Timeout to
        // InfiniteTimeSpan prevents the inner HttpClient timeout from racing the SDK cancellation handler.
        builder.Services
            .AddHttpClient(AzureOpenAIProviderFactory.HttpClientName, client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
            });

        builder.Services.AddSingleton<IAIProviderFactory, AzureOpenAIProviderFactory>();

        return builder;
    }
}
