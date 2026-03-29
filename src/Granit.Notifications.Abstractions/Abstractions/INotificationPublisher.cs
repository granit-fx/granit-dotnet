using System.Text.Json;

namespace Granit.Notifications.Abstractions;

/// <summary>
/// Application-facing facade for publishing notification triggers into the dispatch engine.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>
    /// Publishes a notification to explicit recipients.
    /// </summary>
    ValueTask PublishAsync<TData>(
        NotificationType<TData> notificationType,
        TData data,
        IReadOnlyList<string> recipientUserIds,
        CancellationToken cancellationToken = default) where TData : notnull;

    /// <summary>
    /// Publishes a notification to explicit recipients with a related entity reference.
    /// </summary>
    ValueTask PublishAsync<TData>(
        NotificationType<TData> notificationType,
        TData data,
        IReadOnlyList<string> recipientUserIds,
        EntityReference? relatedEntity,
        CancellationToken cancellationToken = default) where TData : notnull;

    /// <summary>
    /// Publishes a notification to all subscribers of the given notification type.
    /// </summary>
    ValueTask PublishToSubscribersAsync<TData>(
        NotificationType<TData> notificationType,
        TData data,
        CancellationToken cancellationToken = default) where TData : notnull;

    /// <summary>
    /// Publishes a notification to all followers of the given entity.
    /// </summary>
    ValueTask PublishToEntityFollowersAsync<TData>(
        NotificationType<TData> notificationType,
        TData data,
        EntityReference relatedEntity,
        CancellationToken cancellationToken = default) where TData : notnull;
}
