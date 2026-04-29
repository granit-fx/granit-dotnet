using System.Threading.Channels;
using Granit.Analytics.Extensions;
using Granit.Dashboards.Extensions;
using Granit.DataExchange.Extensions;
using Granit.Diagnostics;
using Granit.Http.Resilience.Extensions;
using Granit.QueryEngine.Extensions;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Dashboards;
using Granit.Webhooks.Definitions;
using Granit.Webhooks.Diagnostics;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Endpoints;
using Granit.Webhooks.Exports;
using Granit.Webhooks.Handlers;
using Granit.Webhooks.Internal;
using Granit.Webhooks.Messages;
using Granit.Webhooks.Metrics;
using Granit.Webhooks.Options;
using Granit.Webhooks.Queries;
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
        // ConnectCallback enforces SSRF protection at DNS resolution time (CWE-918 / DNS rebinding).
        builder.Services.AddGranitHttpClient(WebhooksConstants.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds);
            client.DefaultRequestHeaders.Add(
                "User-Agent", $"Granit-Webhooks/{WebhooksConstants.ApiVersion}");
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            ConnectCallback = WebhookSsrfConnectCallback.ConnectAsync,
        });

        // Default (replaceable) store registrations.
        builder.Services.AddSingleton<InMemoryWebhookSubscriptionStore>();
        builder.Services.AddSingleton<IWebhookSubscriptionReader>(sp => sp.GetRequiredService<InMemoryWebhookSubscriptionStore>());
        builder.Services.AddSingleton<IWebhookSubscriptionWriter>(sp => sp.GetRequiredService<InMemoryWebhookSubscriptionStore>());
        builder.Services.AddSingleton<IWebhookSigningKeyReader>(sp => sp.GetRequiredService<InMemoryWebhookSubscriptionStore>());
        builder.Services.AddSingleton<IWebhookSigningKeyWriter>(sp => sp.GetRequiredService<InMemoryWebhookSubscriptionStore>());
        builder.Services.AddScoped<IWebhookDeliveryWriter, NullWebhookDeliveryWriter>();
        builder.Services.AddScoped<IWebhookDeliveryReader, NullWebhookDeliveryReader>();
        // Default: pass-through protector suitable for dev/test.
        // For production (ISO 27001 A.8.24), register EncryptionWebhookSecretProtector
        // (backed by IStringEncryptionService) BEFORE calling AddGranitWebhooks().
        builder.Services.TryAddSingleton<IWebhookSecretProtector, NoOpWebhookSecretProtector>();

        // Handlers (scoped — required by the Channel-based worker)
        builder.Services.AddScoped<WebhookFanoutHandler>();
        builder.Services.AddScoped<SendWebhookHandler>();

        // In-process channel dispatch (default — replaced by Granit.Webhooks.Wolverine).
        // Bounded channels prevent OOM under event burst (OWASP API4 — Unrestricted Resource Consumption).
        builder.Services.AddSingleton(Channel.CreateBounded<WebhookTrigger>(
            new BoundedChannelOptions(WebhooksConstants.TriggerChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
            }));
        builder.Services.AddSingleton(Channel.CreateBounded<SendWebhookCommand>(
            new BoundedChannelOptions(WebhooksConstants.CommandChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
            }));
        builder.Services.AddScoped<IWebhookPublisher, ChannelWebhookPublisher>();
        builder.Services.AddScoped<IWebhookCommandDispatcher, ChannelWebhookCommandDispatcher>();
        builder.Services.AddHostedService<WebhookDispatchWorker>();

        // Module config provider — used by GET /webhooks/config endpoint.
        builder.Services.AddScoped<WebhookModuleConfigProvider>();

        // Redelivery service — used by admin endpoints.
        builder.Services.AddScoped<RetryWebhookHandler>();

        // Test ping and stats — replaceable defaults.
        builder.Services.AddScoped<IWebhookTestPingService, WebhookTestPingService>();
        builder.Services.AddSingleton<IWebhookStatsReader, NullWebhookStatsReader>();

        // Event type registry — immutable singleton, pre-sorted at startup.
        builder.Services.TryAddSingleton<IWebhookEventTypeRegistry, WebhookEventTypeRegistry>();

        builder.Services.AddQueryDefinition<WebhookSubscription, WebhookSubscriptionQueryDefinition>();
        builder.Services.AddQueryDefinition<WebhookDeliveryAttempt, WebhookDeliveryAttemptQueryDefinition>();
        builder.Services.AddExportDefinition<WebhookSubscription, WebhookSubscriptionExportDefinition>();
        builder.Services.AddExportDefinition<WebhookDeliveryAttempt, WebhookDeliveryAttemptExportDefinition>();

        builder.Services.AddMetricDefinition<WebhookSubscription, int, ActiveWebhookSubscriptionCountMetricDefinition>();
        builder.Services.AddMetricDefinition<WebhookDeliveryAttempt, int, FailedWebhookDeliveryAttemptCountMetricDefinition>();
        builder.Services.AddMetricDefinition<WebhookDeliveryAttempt, double, WebhookDeliverySuccessRateMetricDefinition>();
        builder.Services.AddMetricDefinition<WebhookDeliveryAttempt, double, WebhookDeliveryLatencyAverageMetricDefinition>();

        builder.Services.AddDashboardDefinition<WebhookReliabilityDashboardDefinition>();

        return builder;
    }
}
