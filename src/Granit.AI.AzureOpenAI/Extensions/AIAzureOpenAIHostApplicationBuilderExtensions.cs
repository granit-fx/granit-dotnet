using System.Diagnostics.CodeAnalysis;
using Granit.AI.AzureOpenAI.Diagnostics;
using Granit.AI.AzureOpenAI.Internal;
using Granit.AI.AzureOpenAI.Options;
using Granit.AI.Tenancy;
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
    /// Cascade Workspace → Tenant Setting → Global Setting → Host Options resolved by
    /// <see cref="AzureOpenAICredentialResolver"/>. Managed Identity fallback is opt-in via
    /// <see cref="AzureOpenAIProviderOptions.AllowManagedIdentityFallback"/> (default <c>false</c>).
    /// HttpClient is configured with redirect-follow disabled and a
    /// <see cref="GranitSafeConnectCallback"/> to defeat DNS rebinding.
    /// </remarks>
    public static IHostApplicationBuilder AddGranitAIAzureOpenAI(this IHostApplicationBuilder builder)
    {
        GranitActivitySourceRegistry.Register(AIAzureOpenAIActivitySource.Name);

        builder.Services
            .AddOptions<AzureOpenAIProviderOptions>()
            .BindConfiguration(AzureOpenAIProviderOptions.SectionName)
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<AzureOpenAIProviderOptions>, AzureOpenAIProviderOptionsValidator>();

        builder.Services
            .AddHttpClient(AzureOpenAIProviderFactory.HttpClientName, client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                ConnectCallback = GranitSafeConnectCallback.Create(AIEndpointPolicy.AzureOpenAI),
            });

        builder.Services.AddSingleton<AzureOpenAIClientCache>();
        builder.Services.AddScoped<AzureOpenAICredentialResolver>();
        builder.Services.AddScoped<IAIProviderCredentialResolver>(sp =>
            sp.GetRequiredService<AzureOpenAICredentialResolver>());
        builder.Services.AddScoped<IAIProviderFactory, AzureOpenAIProviderFactory>();

        return builder;
    }
}
