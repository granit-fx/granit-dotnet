using System.Diagnostics.CodeAnalysis;
using Granit.AI.Anthropic.Diagnostics;
using Granit.AI.Anthropic.Handlers;
using Granit.AI.Anthropic.Internal;
using Granit.AI.Anthropic.Options;
using Granit.AI.Tenancy;
using Granit.Diagnostics;
using Granit.Events;
using Granit.Settings.Events;
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
    /// registers options validation, the credential cascade resolver (Workspace &#8594; Tenant &#8594;
    /// Global &#8594; Host Options), the Singleton SDK client cache, the named <see cref="HttpClient"/>,
    /// and the factory (Scoped) as an <see cref="IAIProviderFactory"/> implementation.
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
        // AllowAutoRedirect = false closes the redirect-to-metadata SSRF path (anthropic.com does
        // not legitimately 3xx).
        builder.Services
            .AddHttpClient(AnthropicProviderFactory.HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
            });

        builder.Services.AddSingleton<AnthropicClientCache>();
        builder.Services.AddScoped<AnthropicCredentialResolver>();
        builder.Services.AddScoped<IAIProviderCredentialResolver>(sp =>
            sp.GetRequiredService<AnthropicCredentialResolver>());
        builder.Services.AddScoped<IAIProviderFactory, AnthropicProviderFactory>();

        // Evict cached SDK clients the moment an Anthropic credential setting changes,
        // rather than waiting out the cache's 90s sliding expiration.
        builder.Services.AddScoped<ILocalEventHandler<SettingChangedEvent>, AnthropicCredentialCacheInvalidationHandler>();

        return builder;
    }
}
