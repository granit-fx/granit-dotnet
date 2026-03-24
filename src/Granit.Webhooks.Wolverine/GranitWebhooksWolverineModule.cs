using Granit.Modularity;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Exceptions;
using Granit.Webhooks.Internal;
using Granit.Webhooks.Messages;
using Granit.Webhooks.Options;
using Granit.Webhooks.Wolverine.Internal;
using Granit.Wolverine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolverine;
using Wolverine.ErrorHandling;

namespace Granit.Webhooks.Wolverine;

/// <summary>
/// Granit module that replaces the default Channel-based webhook dispatch with
/// durable Wolverine <see cref="IMessageBus"/>-backed implementations.
/// </summary>
/// <remarks>
/// The in-process <see cref="WebhookDispatchWorker"/> remains registered but stays
/// idle because the Wolverine publishers bypass the channels.
/// </remarks>
[DependsOn(
    typeof(GranitWebhooksModule),
    typeof(GranitWolverineModule))]
public sealed class GranitWebhooksWolverineModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Replace Channel-based implementations with Wolverine IMessageBus.
        context.Services.Replace(ServiceDescriptor
            .Scoped<IWebhookPublisher, WolverineWebhookPublisher>());
        context.Services.Replace(ServiceDescriptor
            .Scoped<IWebhookCommandDispatcher, WolverineWebhookCommandDispatcher>());

        // Resolve options for queue configuration.
        WebhooksOptions options = new();
        context.Configuration
            .GetSection(WebhooksOptions.SectionName)
            .Bind(options);

        // Configure Wolverine local queues and retry policies.
        context.Services.ConfigureWolverine(opts =>
        {
            // Dedicated local queue for HTTP delivery — isolated from the main bus.
            opts.LocalQueueFor<SendWebhookCommand>()
                .Named(WebhooksConstants.DeliveryQueueName)
                .MaximumParallelMessages(options.MaxParallelDeliveries);

            // Exponential backoff for retriable HTTP errors.
            // After 6 retries (~14h30 total), Wolverine moves the message to the Dead-Letter Queue.
            opts.OnException<WebhookDeliveryException>()
                .RetryWithCooldown(
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromMinutes(2),
                    TimeSpan.FromMinutes(10),
                    TimeSpan.FromMinutes(30),
                    TimeSpan.FromHours(2),
                    TimeSpan.FromHours(12));
        });
    }
}
