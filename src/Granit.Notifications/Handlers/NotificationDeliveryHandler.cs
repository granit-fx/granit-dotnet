using System.Diagnostics;
using System.Runtime.ExceptionServices;
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

        // Idempotency: skip if this delivery was already successfully finalized (retry safety).
        if (await deliveryWriter.HasBeenDeliveredAsync(command.DeliveryId, cancellationToken)
                .ConfigureAwait(false))
        {
            LogDuplicateDeliverySkipped(command.ChannelName, command.DeliveryId, command.NotificationId);
            return;
        }

        INotificationChannel? channel = channels.FirstOrDefault(c => c.Name == command.ChannelName);

        if (channel is null)
        {
            LogChannelNotRegistered(command.ChannelName, command.DeliveryId, command.NotificationId);
            return;
        }

        NotificationDeliveryAttempt claim = new()
        {
            Id = guidGenerator.Create(),
            DeliveryId = command.DeliveryId,
            NotificationId = command.NotificationId,
            NotificationTypeName = command.NotificationTypeName,
            ChannelName = command.ChannelName,
            RecipientUserId = command.RecipientUserId,
            TenantId = command.TenantId,
            OccurredAt = clock.Now,
            DurationMs = 0,
            IsSuccess = null,
            ErrorMessage = null,
        };

        if (!await deliveryWriter.TryAcquireDeliveryAttemptAsync(claim, cancellationToken).ConfigureAwait(false))
        {
            LogConcurrentDeliverySkipped(command.ChannelName, command.DeliveryId, command.NotificationId);
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
            RecipientOverride = command.RecipientOverride,
        };

        var stopwatch = Stopwatch.StartNew();
        bool sent = false;
        string? errorMessage = null;
        ExceptionDispatchInfo? failure = null;
        try
        {
            await channel.SendAsync(context, cancellationToken).ConfigureAwait(false);
            sent = true;
            stopwatch.Stop();
            activity?.SetTag("notifications.success", true);

            metrics.RecordDeliverySucceeded(
                command.TenantId?.ToString(), command.ChannelName, command.NotificationTypeName);
            metrics.RecordDeliveryDuration(
                command.TenantId?.ToString(), command.ChannelName, "success", stopwatch.Elapsed);

            LogNotificationDelivered(command.ChannelName, command.DeliveryId, command.NotificationId);
        }
        catch (OperationCanceledException oce)
        {
            // Claim already persisted — finalize so the row cannot block transport retries as
            // stuck "pending". MUST use CancellationToken.None: the inbound token is already
            // cancelled, and propagating it would cause ExecuteUpdateAsync to throw immediately,
            // leaving the audit row at IsSuccess=null and recreating the very stuck-pending state
            // this handler is trying to avoid.
            stopwatch.Stop();
            await TryFinalizeAuditAsync(
                command,
                success: false,
                stopwatch.ElapsedMilliseconds,
                errorMessage: oce.Message,
                CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetTag("notifications.success", false);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            metrics.RecordDeliveryFailed(
                command.TenantId?.ToString(), command.ChannelName, command.NotificationTypeName);
            metrics.RecordDeliveryDuration(
                command.TenantId?.ToString(), command.ChannelName, "failure", stopwatch.Elapsed);

            LogNotificationDeliveryFailed(ex, command.ChannelName, command.DeliveryId, command.NotificationId);

            errorMessage = ex.Message;
            failure = ExceptionDispatchInfo.Capture(new NotificationDeliveryException(
                $"Failed to deliver notification {command.NotificationId} via {command.ChannelName}", ex));
        }

        await TryFinalizeAuditAsync(
            command,
            sent,
            stopwatch.ElapsedMilliseconds,
            errorMessage,
            cancellationToken).ConfigureAwait(false);

        failure?.Throw();
    }

    private async Task TryFinalizeAuditAsync(
        DeliverNotificationCommand command,
        bool success,
        long durationMilliseconds,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            await deliveryWriter.CompleteDeliveryAttemptAsync(
                command.DeliveryId,
                success,
                durationMilliseconds,
                errorMessage,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogAuditFinalizeFailed(ex, command.ChannelName, command.DeliveryId);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Duplicate delivery {DeliveryId} skipped for notification {NotificationId} via '{ChannelName}' (already delivered)")]
    private partial void LogDuplicateDeliverySkipped(string channelName, Guid deliveryId, Guid notificationId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Delivery {DeliveryId} for notification {NotificationId} via '{ChannelName}' skipped — another worker claimed or is executing this outbound attempt")]
    private partial void LogConcurrentDeliverySkipped(string channelName, Guid deliveryId, Guid notificationId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Notification channel '{ChannelName}' is not registered — skipping delivery {DeliveryId} for notification {NotificationId}")]
    private partial void LogChannelNotRegistered(string channelName, Guid deliveryId, Guid notificationId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Notification delivered via '{ChannelName}' for delivery {DeliveryId} notification {NotificationId}")]
    private partial void LogNotificationDelivered(string channelName, Guid deliveryId, Guid notificationId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Notification delivery failed via '{ChannelName}' for delivery {DeliveryId} notification {NotificationId}")]
    private partial void LogNotificationDeliveryFailed(Exception exception, string channelName, Guid deliveryId, Guid notificationId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to finalize delivery audit for '{ChannelName}' delivery {DeliveryId} — the outbound message may already have been transmitted")]
    private partial void LogAuditFinalizeFailed(Exception exception, string channelName, Guid deliveryId);
}
