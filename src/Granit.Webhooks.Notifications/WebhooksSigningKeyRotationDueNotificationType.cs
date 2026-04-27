using Granit.Notifications;

namespace Granit.Webhooks.Notifications;

/// <summary>
/// Notification type raised when a webhook signing key is approaching expiration and
/// administrators need to rotate it before downstream consumers stop being able to
/// verify signatures. Triggered by the daily rotation scanner (FU-1b).
/// </summary>
/// <remarks>
/// Channels: Email + InApp. <see cref="NotificationSeverity.Warning"/> because the
/// key is still accepted at this point — the lead-time window (default 14 days) gives
/// administrators ample notice before signature verification breaks. Recipients are
/// resolved via the notification subscription system (admins opt in through the
/// admin UI), keeping this bridge free of any tenant-admin lookup service.
/// </remarks>
public sealed class WebhooksSigningKeyRotationDueNotificationType
    : NotificationType<WebhooksSigningKeyRotationDueNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly WebhooksSigningKeyRotationDueNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "webhooks.signing_key_rotation_due";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>
/// Data payload for the webhook signing-key rotation-due notification.
/// </summary>
/// <param name="SubscriptionId">Identifier of the owning webhook subscription.</param>
/// <param name="KeyId">Identifier of the signing key approaching expiration.</param>
/// <param name="ExpiresAt">UTC timestamp at which the key stops being accepted.</param>
/// <param name="DaysUntilExpiry">Whole days remaining between the scanner run and
/// <paramref name="ExpiresAt"/>. Computed once by the handler and exposed to the
/// template so the rendered email can lead with a human-friendly countdown.</param>
public sealed record WebhooksSigningKeyRotationDueNotificationData(
    Guid SubscriptionId,
    Guid KeyId,
    DateTimeOffset ExpiresAt,
    int DaysUntilExpiry);
