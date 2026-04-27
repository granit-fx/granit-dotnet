using Granit.Notifications;

namespace Granit.Authentication.ApiKeys.Notifications;

/// <summary>
/// Notification type fired when a new API key has been created — sent to tenant
/// administrators so the issuance is visible without a manual audit-log dive.
/// Helps catch accidental or malicious provisioning and supports ISO 27001 A.9.2
/// (user access provisioning).
/// </summary>
/// <remarks>
/// Channels: Email + InApp. <see cref="NotificationSeverity.Info"/> — issuance is
/// expected operational activity, not an alert. The data payload carries only public-safe
/// metadata: <see cref="ApiKeyIssuedNotificationData.KeyId"/> /
/// <see cref="ApiKeyIssuedNotificationData.KeyName"/> /
/// <see cref="ApiKeyIssuedNotificationData.KeyType"/>. The key secret and its SHA-256
/// hash never reach the notification payload.
/// </remarks>
public sealed class ApiKeyIssuedNotificationType
    : NotificationType<ApiKeyIssuedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly ApiKeyIssuedNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "apikeys.new_key_issued";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Info;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>
/// Data payload for an "API key issued" notification.
/// </summary>
/// <remarks>
/// Fields are deliberately limited to public-safe metadata. The raw key value and its
/// SHA-256 hash are NEVER included — defense in depth against accidental exposure
/// through email logs, in-app history, or admin UI.
/// </remarks>
/// <param name="KeyId">Stable identifier of the new API key.</param>
/// <param name="KeyName">Human-readable display name (e.g. <c>Partner Lab X</c>).</param>
/// <param name="KeyType">Key category (Secret, Publishable, Webhook, Ephemeral).</param>
public sealed record ApiKeyIssuedNotificationData(
    Guid KeyId,
    string KeyName,
    ApiKeyType KeyType);
