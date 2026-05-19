using System.Diagnostics.CodeAnalysis;
using Granit.AI.OpenAI.Diagnostics;
using Granit.AI.OpenAI.Internal;
using Granit.AI.OpenAI.Options;
using Granit.AI.Tenancy;
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
    /// The API key may be supplied at the Host (options), Tenant (Setting), or Workspace level —
    /// the cascade is enforced by <see cref="OpenAICredentialResolver"/>. SSRF protection is
    /// applied to any tenant-supplied <c>Endpoint</c> via <see cref="AIEndpointValidator"/> plus a
    /// <see cref="GranitSafeConnectCallback"/> on the named <c>HttpClient</c>. Redirect follow is
    /// disabled.
    /// </remarks>
    public static IHostApplicationBuilder AddGranitAIOpenAI(this IHostApplicationBuilder builder)
    {
        GranitActivitySourceRegistry.Register(AIOpenAIActivitySource.Name);

        builder.Services
            .AddOptions<OpenAIProviderOptions>()
            .BindConfiguration(OpenAIProviderOptions.SectionName)
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<OpenAIProviderOptions>, OpenAIProviderOptionsValidator>();

        builder.Services.TryAddSingleton(TimeProvider.System);

        builder.Services
            .AddHttpClient(OpenAIProviderFactory.HttpClientName, client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                ConnectCallback = GranitSafeConnectCallback.Create(AIEndpointPolicy.HostedHttps),
            });

        builder.Services.AddSingleton<OpenAIClientCache>();
        builder.Services.AddScoped<OpenAICredentialResolver>();
        builder.Services.AddScoped<IAIProviderCredentialResolver>(sp =>
            sp.GetRequiredService<OpenAICredentialResolver>());
        builder.Services.AddScoped<IAIProviderFactory, OpenAIProviderFactory>();

        return builder;
    }
}
