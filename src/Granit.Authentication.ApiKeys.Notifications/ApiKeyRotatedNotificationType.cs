using Granit.Notifications;

namespace Granit.Authentication.ApiKeys.Notifications;

/// <summary>
/// Notification type fired when an API key has been rotated — the old key remains
/// active during the configured grace period. Sent to tenant administrators so they
/// can confirm the rotation completed and downstream consumers picked up the new key.
/// Supports ISO 27001 A.9.4.3 (secret authentication information management).
/// </summary>
/// <remarks>
/// Channels: Email + InApp. <see cref="NotificationSeverity.Info"/> — rotation is
/// expected operational activity. The payload carries only the old / new key
/// identifiers; neither raw key nor hash is propagated.
/// </remarks>
public sealed class ApiKeyRotatedNotificationType
    : NotificationType<ApiKeyRotatedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly ApiKeyRotatedNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "apikeys.rotation_completed";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Info;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>
/// Data payload for an "API key rotated" notification.
/// </summary>
/// <remarks>
/// The originating <c>ApiKeyRotatedEto</c> carries the old key's SHA-256 hash for
/// cache invalidation purposes. That hash is intentionally NOT propagated here —
/// notification recipients (administrators) have no use for it, and excluding it
/// avoids leaking sensitive material into email transcripts and in-app history.
/// </remarks>
/// <param name="OldKeyId">Identifier of the key being replaced.</param>
/// <param name="NewKeyId">Identifier of the newly issued key.</param>
public sealed record ApiKeyRotatedNotificationData(
    Guid OldKeyId,
    Guid NewKeyId);
