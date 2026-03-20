using System.Diagnostics;
using Granit.Guids;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Diagnostics;
using Granit.Notifications.Domain;
using Granit.Notifications.Exceptions;
using Granit.Notifications.Messages;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.Notifications.Handlers;

/// <summary>
/// Wolverine handler that delivers a <see cref="DeliverNotificationCommand"/> via
/// the appropriate <see cref="INotificationChannel"/>.
/// </summary>
public sealed partial class NotificationDeliveryHandler(
    IEnumerable<INotificationChannel> channels,
    INotificationDeliveryWriter deliveryWriter,
    IGuidGenerator guidGenerator,
    IClock clock,
    ILogger<NotificationDeliveryHandler> logger,
    NotificationsMetrics metrics)
{
    /// <summary>
    /// Routes delivery to the matching channel. Channels not registered are skipped
    /// with a warning (NestJS graceful degradation pattern).
    /// </summary>
    public async Task HandleAsync(DeliverNotificationCommand command, CancellationToken cancellationToken)
    {
        using Activity? activity = NotificationsActivitySource.Source.StartActivity(NotificationsActivitySource.Deliver);
        activity?.SetTag("notifications.channel", command.ChannelName);
        activity?.SetTag("notifications.delivery_id", command.DeliveryId.ToString());
        activity?.SetTag("notifications.notification_id", command.NotificationId.ToString());
        activity?.SetTag("notifications.type", command.NotificationTypeName);

        INotificationChannel? channel = channels.FirstOrDefault(c => c.Name == command.ChannelName);

        if (channel is null)
        {
            LogChannelNotRegistered(command.ChannelName, command.DeliveryId, command.NotificationId);
            return;
        }

        NotificationDeliveryContext context = new()
        {
            NotificationId = command.NotificationId,
            DeliveryId = command.DeliveryId,
            NotificationTypeName = command.NotificationTypeName,
            Severity = command.Severity,
            RecipientUserId = command.RecipientUserId,
            Data = command.Data,
            RelatedEntity = command.RelatedEntity,
            TenantId = command.TenantId,
            OccurredAt = command.OccurredAt,
            Culture = command.Culture,
        };

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await channel.SendAsync(context, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            activity?.SetTag("notifications.success", true);

            await deliveryWriter.RecordAsync(new NotificationDeliveryAttempt
            {
                Id = guidGenerator.Create(),
                DeliveryId = command.DeliveryId,
                NotificationId = command.NotificationId,
                NotificationTypeName = command.NotificationTypeName,
                ChannelName = command.ChannelName,
                RecipientUserId = command.RecipientUserId,
                TenantId = command.TenantId,
                OccurredAt = clock.Now,
                DurationMs = stopwatch.ElapsedMilliseconds,
                IsSuccess = true,
            }, cancellationToken).ConfigureAwait(false);

            metrics.RecordDeliverySucceeded(
                command.TenantId?.ToString(), command.ChannelName, command.NotificationTypeName);
            metrics.RecordDeliveryDuration(
                command.TenantId?.ToString(), command.ChannelName, "success", stopwatch.Elapsed);

            LogNotificationDelivered(command.ChannelName, command.DeliveryId, command.NotificationId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            activity?.SetTag("notifications.success", false);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            await deliveryWriter.RecordAsync(new NotificationDeliveryAttempt
            {
                Id = guidGenerator.Create(),
                DeliveryId = command.DeliveryId,
                NotificationId = command.NotificationId,
                NotificationTypeName = command.NotificationTypeName,
                ChannelName = command.ChannelName,
                RecipientUserId = command.RecipientUserId,
                TenantId = command.TenantId,
                OccurredAt = clock.Now,
                DurationMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message,
                IsSuccess = false,
            }, cancellationToken).ConfigureAwait(false);

            metrics.RecordDeliveryFailed(
                command.TenantId?.ToString(), command.ChannelName, command.NotificationTypeName);
            metrics.RecordDeliveryDuration(
                command.TenantId?.ToString(), command.ChannelName, "failure", stopwatch.Elapsed);

            LogNotificationDeliveryFailed(ex, command.ChannelName, command.DeliveryId, command.NotificationId);

            throw new NotificationDeliveryException(
                $"Failed to deliver notification {command.NotificationId} via {command.ChannelName}", ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Notification channel '{ChannelName}' is not registered — skipping delivery {DeliveryId} for notification {NotificationId}")]
    private partial void LogChannelNotRegistered(string channelName, Guid deliveryId, Guid notificationId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Notification delivered via '{ChannelName}' for delivery {DeliveryId} notification {NotificationId}")]
    private partial void LogNotificationDelivered(string channelName, Guid deliveryId, Guid notificationId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Notification delivery failed via '{ChannelName}' for delivery {DeliveryId} notification {NotificationId}")]
    private partial void LogNotificationDeliveryFailed(Exception exception, string channelName, Guid deliveryId, Guid notificationId);
}
