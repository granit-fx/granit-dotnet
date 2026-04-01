using System.Text.Json;
using System.Threading.Channels;
using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Messages;
using Granit.Timing;

namespace Granit.Notifications.Internal;

/// <summary>
/// Default <see cref="INotificationPublisher"/> implementation that writes
/// <see cref="NotificationTrigger"/> messages to an in-process channel consumed
/// by <see cref="NotificationDispatchWorker"/>.
/// </summary>
/// <remarks>
/// Replaced by the Wolverine-backed publisher when <c>Granit.Notifications.Wolverine</c>
/// is loaded (durable outbox dispatch).
/// </remarks>
internal sealed class ChannelNotificationPublisher(
    Channel<NotificationTrigger> channel,
    ICurrentTenant currentTenant,
    IClock clock) : INotificationPublisher
{
    public ValueTask PublishAsync<TData>(
        NotificationType<TData> notificationType,
        TData data,
        IReadOnlyList<string> recipientUserIds,
        CancellationToken cancellationToken = default) where TData : notnull =>
        PublishAsync(notificationType, data, recipientUserIds, relatedEntity: null, cancellationToken);

    public async ValueTask PublishAsync<TData>(
        NotificationType<TData> notificationType,
        TData data,
        IReadOnlyList<string> recipientUserIds,
        EntityReference? relatedEntity,
        CancellationToken cancellationToken = default) where TData : notnull
    {
        NotificationTrigger trigger = BuildTrigger(notificationType, data, relatedEntity);
        trigger = trigger with { RecipientUserIds = recipientUserIds };
        await channel.Writer.WriteAsync(trigger, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask PublishAsync<TData>(
        NotificationType<TData> notificationType,
        TData data,
        IReadOnlyList<string> recipientUserIds,
        RecipientInfo recipientOverride,
        EntityReference? relatedEntity = null,
        CancellationToken cancellationToken = default) where TData : notnull
    {
        ArgumentNullException.ThrowIfNull(recipientOverride);
        if (recipientUserIds.Count > 1)
        {
            throw new ArgumentException(
                "RecipientOverride supports only a single recipient.", nameof(recipientUserIds));
        }

        NotificationTrigger trigger = BuildTrigger(notificationType, data, relatedEntity);
        trigger = trigger with { RecipientUserIds = recipientUserIds, RecipientOverride = recipientOverride };
        await channel.Writer.WriteAsync(trigger, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask PublishToSubscribersAsync<TData>(
        NotificationType<TData> notificationType,
        TData data,
        CancellationToken cancellationToken = default) where TData : notnull
    {
        NotificationTrigger trigger = BuildTrigger(notificationType, data, relatedEntity: null);
        await channel.Writer.WriteAsync(trigger, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask PublishToEntityFollowersAsync<TData>(
        NotificationType<TData> notificationType,
        TData data,
        EntityReference relatedEntity,
        CancellationToken cancellationToken = default) where TData : notnull
    {
        NotificationTrigger trigger = BuildTrigger(notificationType, data, relatedEntity);
        await channel.Writer.WriteAsync(trigger, cancellationToken).ConfigureAwait(false);
    }

    private NotificationTrigger BuildTrigger<TData>(
        NotificationType<TData> notificationType,
        TData data,
        EntityReference? relatedEntity) where TData : notnull => new()
        {
            NotificationTypeName = notificationType.Name,
            Severity = notificationType.DefaultSeverity,
            Data = JsonSerializer.SerializeToElement(data),
            RelatedEntity = relatedEntity,
            TenantId = currentTenant.IsAvailable ? currentTenant.Id : null,
            OccurredAt = clock.Now,
        };
}
