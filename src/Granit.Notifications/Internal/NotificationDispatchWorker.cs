using System.Threading.Channels;
using Granit.Notifications.Exceptions;
using Granit.Notifications.Handlers;
using Granit.Notifications.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.Notifications.Internal;

/// <summary>
/// Background service that reads <see cref="NotificationTrigger"/> from the channel,
/// executes fan-out via <see cref="NotificationFanoutHandler"/>, then delivers each
/// command via <see cref="NotificationDeliveryHandler"/>.
/// </summary>
/// <remarks>
/// This is the default in-process dispatch path. When <c>Granit.Notifications.Wolverine</c>
/// is loaded, this worker is removed and Wolverine handles dispatch via durable queues.
/// </remarks>
internal sealed partial class NotificationDispatchWorker(
    Channel<NotificationTrigger> channel,
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationDispatchWorker> logger) : BackgroundService
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
    ];

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (NotificationTrigger trigger in channel.Reader.ReadAllAsync(stoppingToken))
            {
                await DispatchTriggerAsync(trigger, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested — fall through to drain remaining buffered triggers.
        }

        // Graceful drain: StopAsync has completed the writer; no new triggers will arrive.
        // Bounded by the host ShutdownTimeout (default 5 s) — raise it if needed.
        while (channel.Reader.TryRead(out NotificationTrigger? trigger))
        {
            await DispatchTriggerAsync(trigger, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        channel.Writer.TryComplete();
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task DispatchTriggerAsync(NotificationTrigger trigger, CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            NotificationFanoutHandler fanout =
                scope.ServiceProvider.GetRequiredService<NotificationFanoutHandler>();

            IEnumerable<DeliverNotificationCommand> commands =
                await fanout.HandleAsync(trigger, cancellationToken).ConfigureAwait(false);

            NotificationDeliveryHandler delivery =
                scope.ServiceProvider.GetRequiredService<NotificationDeliveryHandler>();

            foreach (DeliverNotificationCommand command in commands)
            {
                await DeliverWithRetryAsync(delivery, command, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFanoutFailed(trigger.NotificationTypeName, trigger.NotificationId, ex);
        }
    }

    private async Task DeliverWithRetryAsync(
        NotificationDeliveryHandler handler,
        DeliverNotificationCommand command,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (NotificationDeliveryException ex) when (attempt < MaxRetries)
            {
                LogDeliveryRetry(command.ChannelName, command.DeliveryId, attempt + 1, MaxRetries, ex);
                await Task.Delay(RetryDelays[attempt], cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogDeliveryFailed(command.ChannelName, command.DeliveryId, command.NotificationId, ex);
                return;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Notification fan-out failed for type '{NotificationTypeName}' notification {NotificationId}")]
    private partial void LogFanoutFailed(string notificationTypeName, Guid notificationId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Notification delivery via '{ChannelName}' for {DeliveryId} failed (attempt {Attempt}/{MaxRetries}), retrying")]
    private partial void LogDeliveryRetry(string channelName, Guid deliveryId, int attempt, int maxRetries, Exception exception);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Notification delivery via '{ChannelName}' for {DeliveryId} notification {NotificationId} failed permanently")]
    private partial void LogDeliveryFailed(string channelName, Guid deliveryId, Guid notificationId, Exception exception);
}
