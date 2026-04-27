using Granit.Notifications;

namespace Granit.Authentication.ApiKeys.Notifications;

/// <summary>
/// Notification type fired when an API key is approaching expiration. Routed by the
/// daily scanner in <c>Granit.Authentication.ApiKeys.BackgroundJobs</c> so tenant
/// administrators can rotate ahead of disruption — supporting ISO 27001 A.9.4
/// (least-privilege rotation policy).
/// </summary>
/// <remarks>
/// Channels: Email + InApp. <see cref="NotificationSeverity.Warning"/> — service
/// disruption is imminent if no rotation happens. The data payload carries only
/// public-safe metadata: <see cref="ApiKeyExpiringSoonNotificationData.KeyId"/>,
/// <see cref="ApiKeyExpiringSoonNotificationData.KeyName"/>,
/// <see cref="ApiKeyExpiringSoonNotificationData.KeyType"/>,
/// <see cref="ApiKeyExpiringSoonNotificationData.ExpiresAt"/>,
/// <see cref="ApiKeyExpiringSoonNotificationData.DaysUntilExpiry"/>. The hash, raw
/// value and prefix are never included.
/// </remarks>
public sealed class ApiKeyExpiringSoonNotificationType
    : NotificationType<ApiKeyExpiringSoonNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly ApiKeyExpiringSoonNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "apikeys.expiring_soon";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>
/// Data payload for an "API key expiring soon" notification.
/// </summary>
/// <remarks>
/// Fields are deliberately limited to public-safe metadata. The raw key value, its
/// SHA-256 hash, and even the on-the-wire prefix are NEVER included — defence in
/// depth against accidental exposure through email logs, in-app history, or admin
/// UI captures. <see cref="DaysUntilExpiry"/> is precomputed by the bridge handler
/// so templates do not have to reach into <see cref="DateTimeOffset"/> arithmetic.
/// </remarks>
/// <param name="KeyId">Stable identifier of the expiring API key.</param>
/// <param name="KeyName">Human-readable display name (e.g. <c>Partner Lab X</c>).</param>
/// <param name="KeyType">Key category (Secret, Publishable, Webhook, Ephemeral).</param>
/// <param name="ExpiresAt">UTC instant at which the key stops being accepted.</param>
/// <param name="DaysUntilExpiry">Whole days remaining (rounded down, clamped to 0).</param>
public sealed record ApiKeyExpiringSoonNotificationData(
    Guid KeyId,
    string KeyName,
    ApiKeyType KeyType,
    DateTimeOffset ExpiresAt,
    int DaysUntilExpiry);
