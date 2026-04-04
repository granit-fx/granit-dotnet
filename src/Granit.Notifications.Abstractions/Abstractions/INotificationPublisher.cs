using System.Text.Json;
using Granit.Domain;

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
    /// Publishes a notification to explicit recipients with an overridden contact info.
    /// </summary>
    /// <remarks>
    /// When <paramref name="recipientOverride"/> is provided, the notification pipeline
    /// uses it directly instead of calling <c>IRecipientResolver.ResolveAsync</c>.
    /// Useful for sending to addresses not yet stored in the identity system
    /// (email change confirmation, invitations, pre-registration).
    /// <para>
    /// <b>Constraint:</b> only one recipient is supported when using an override.
    /// If <paramref name="recipientUserIds"/> contains more than one ID and
    /// <paramref name="recipientOverride"/> is not <see langword="null"/>,
    /// an <see cref="ArgumentException"/> is thrown.
    /// </para>
    /// </remarks>
    ValueTask PublishAsync<TData>(
        NotificationType<TData> notificationType,
        TData data,
        IReadOnlyList<string> recipientUserIds,
        RecipientInfo recipientOverride,
        EntityReference? relatedEntity = null,
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
