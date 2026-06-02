using System.Threading.Channels;
using Granit.Webhooks.Exceptions;
using Granit.Webhooks.Handlers;
using Granit.Webhooks.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.Webhooks.Internal;

/// <summary>
/// Background service that reads <see cref="WebhookTrigger"/> and <see cref="SendWebhookCommand"/>
/// from channels, executing fan-out and delivery in-process.
/// </summary>
/// <remarks>
/// When <c>Granit.Webhooks.Wolverine</c> is loaded, this worker stays idle because the
/// Wolverine publisher bypasses the channels.
/// </remarks>
internal sealed partial class WebhookDispatchWorker(
    Channel<WebhookTrigger> triggerChannel,
    Channel<SendWebhookCommand> commandChannel,
    IServiceScopeFactory scopeFactory,
    ILogger<WebhookDispatchWorker> logger) : BackgroundService
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
    ];

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Process both channels concurrently.
        Task triggerTask = ProcessTriggersAsync(stoppingToken);
        Task commandTask = ProcessCommandsAsync(stoppingToken);
        await Task.WhenAll(triggerTask, commandTask).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Seal the trigger channel. ProcessTriggersAsync drains any buffered triggers
        // and then seals commandChannel, allowing ProcessCommandsAsync to drain cleanly.
        triggerChannel.Writer.TryComplete();
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ProcessTriggersAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (WebhookTrigger trigger in triggerChannel.Reader.ReadAllAsync(stoppingToken))
            {
                await FanoutTriggerAsync(trigger, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested — drain remaining buffered triggers before sealing.
        }

        // Graceful drain: write resulting commands into commandChannel (writer still open).
        while (triggerChannel.Reader.TryRead(out WebhookTrigger? trigger))
        {
            await FanoutTriggerAsync(trigger, CancellationToken.None).ConfigureAwait(false);
        }

        // All triggers drained — seal commandChannel so ProcessCommandsAsync finishes.
        commandChannel.Writer.TryComplete();
    }

    private async Task ProcessCommandsAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (SendWebhookCommand command in commandChannel.Reader.ReadAllAsync(stoppingToken))
            {
                await DeliverWithRetryAsync(command, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested — drain commands produced by the trigger drain.
            // ReadAllAsync(None) completes once ProcessTriggersAsync seals commandChannel.
        }

        await foreach (SendWebhookCommand command in commandChannel.Reader.ReadAllAsync(CancellationToken.None))
        {
            await DeliverWithRetryAsync(command, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task FanoutTriggerAsync(WebhookTrigger trigger, CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            WebhookFanoutHandler fanout =
                scope.ServiceProvider.GetRequiredService<WebhookFanoutHandler>();

            IEnumerable<SendWebhookCommand> commands =
                await fanout.HandleAsync(trigger, cancellationToken).ConfigureAwait(false);

            foreach (SendWebhookCommand command in commands)
            {
                await commandChannel.Writer.WriteAsync(command, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFanoutFailed(trigger.EventType, trigger.EventId, ex);
        }
    }

    private async Task DeliverWithRetryAsync(
        SendWebhookCommand command,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                SendWebhookHandler handler =
                    scope.ServiceProvider.GetRequiredService<SendWebhookHandler>();
                await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (WebhookDeliveryException ex) when (attempt < MaxRetries)
            {
                LogDeliveryRetry(command.DeliveryId, attempt + 1, MaxRetries, ex);
                await Task.Delay(RetryDelays[attempt], cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogDeliveryFailed(command.DeliveryId, command.SubscriptionId, ex);
                return;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Webhook fan-out failed for event type '{EventType}' event {EventId}")]
    private partial void LogFanoutFailed(string eventType, Guid eventId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Webhook delivery {DeliveryId} failed (attempt {Attempt}/{MaxRetries}), retrying")]
    private partial void LogDeliveryRetry(Guid deliveryId, int attempt, int maxRetries, Exception exception);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Webhook delivery {DeliveryId} for subscription {SubscriptionId} failed permanently")]
    private partial void LogDeliveryFailed(Guid deliveryId, Guid subscriptionId, Exception exception);
}
