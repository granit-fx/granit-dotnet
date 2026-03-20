using System.Threading.Channels;
using Granit.Core.Diagnostics;
using Granit.Http.Resilience.Extensions;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Diagnostics;
using Granit.Webhooks.Endpoints;
using Granit.Webhooks.Handlers;
using Granit.Webhooks.Internal;
using Granit.Webhooks.Messages;
using Granit.Webhooks.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.Webhooks.Extensions;

/// <summary>
/// Extension methods for registering Granit.Webhooks services.
/// </summary>
public static class WebhooksHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Granit webhook dispatch engine.
    /// </summary>
    /// <remarks>
    /// By default, webhooks are dispatched via in-process <see cref="Channel{T}"/>
    /// consumed by a <see cref="BackgroundService"/>. For durable outbox-backed dispatch,
    /// add the <c>Granit.Webhooks.Wolverine</c> package.
    /// </remarks>
    public static IHostApplicationBuilder AddGranitWebhooks(
        this IHostApplicationBuilder builder,
        Action<WebhooksOptions>? configure = null)
    {
        GranitActivitySourceRegistry.Register(Diagnostics.WebhooksActivitySource.Name);
        builder.Services.TryAddSingleton<WebhooksMetrics>();

        // Bind and validate options at startup.
        builder.Services
            .AddOptions<WebhooksOptions>()
            .BindConfiguration(WebhooksOptions.SectionName)
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<WebhooksOptions>, WebhooksOptionsValidator>();

        // Read options directly from IConfiguration — DI container not yet built.
        WebhooksOptions options = new();
        builder.Configuration
            .GetSection(WebhooksOptions.SectionName)
            .Bind(options);
        configure?.Invoke(options);

        // Named HttpClient for webhook delivery — resilience pipeline + strict timeout.
        builder.Services.AddGranitHttpClient(WebhooksConstants.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds);
            client.DefaultRequestHeaders.Add(
                "User-Agent", $"Granit-Webhooks/{WebhooksConstants.ApiVersion}");
        });

        // Default (replaceable) store registrations.
        builder.Services.AddSingleton<InMemoryWebhookSubscriptionStore>();
        builder.Services.AddSingleton<IWebhookSubscriptionReader>(sp => sp.GetRequiredService<InMemoryWebhookSubscriptionStore>());
        builder.Services.AddSingleton<IWebhookSubscriptionWriter>(sp => sp.GetRequiredService<InMemoryWebhookSubscriptionStore>());
        builder.Services.AddScoped<IWebhookDeliveryWriter, NullWebhookDeliveryWriter>();
        builder.Services.AddScoped<IWebhookDeliveryReader, NullWebhookDeliveryReader>();
        builder.Services.AddSingleton<IWebhookSecretProtector, NoOpWebhookSecretProtector>();

        // Handlers (scoped — required by the Channel-based worker)
        builder.Services.AddScoped<WebhookFanoutHandler>();
        builder.Services.AddScoped<SendWebhookHandler>();

        // In-process channel dispatch (default — replaced by Granit.Webhooks.Wolverine)
        builder.Services.AddSingleton(Channel.CreateUnbounded<WebhookTrigger>());
        builder.Services.AddSingleton(Channel.CreateUnbounded<SendWebhookCommand>());
        builder.Services.AddScoped<IWebhookPublisher, ChannelWebhookPublisher>();
        builder.Services.AddScoped<IWebhookCommandDispatcher, ChannelWebhookCommandDispatcher>();
        builder.Services.AddHostedService<WebhookDispatchWorker>();

        // Module config provider — used by GET /webhooks/config endpoint.
        builder.Services.AddScoped<WebhookModuleConfigProvider>();

        // Redelivery service — used by admin endpoints.
        builder.Services.AddScoped<RetryWebhookHandler>();

        // Test ping, stats, and queryable provider — replaceable defaults.
        builder.Services.AddScoped<IWebhookTestPingService, WebhookTestPingService>();
        builder.Services.AddSingleton<IWebhookStatsReader, NullWebhookStatsReader>();
        builder.Services.AddSingleton<IWebhookQueryableProvider, NullWebhookQueryableProvider>();

        return builder;
    }
}
