using Granit.Notifications;

namespace Granit.Authentication.ApiKeys.Notifications;

/// <summary>
/// Notification type fired when an API key has been revoked — sent to tenant
/// administrators so out-of-band revocations (incident response, employee offboarding,
/// suspected compromise) are visible and traceable. Supports ISO 27001 A.9.2.6
/// (removal or adjustment of access rights).
/// </summary>
/// <remarks>
/// Channels: Email + InApp. <see cref="NotificationSeverity.Info"/> — revocation
/// itself is not an alert; the alert (if any) belongs to whichever event triggered
/// the revocation. The payload carries only the revoked key's identifier — the
/// SHA-256 hash present on the originating <c>ApiKeyRevokedEto</c> is intentionally
/// NOT propagated.
/// </remarks>
public sealed class ApiKeyRevokedNotificationType
    : NotificationType<ApiKeyRevokedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly ApiKeyRevokedNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "apikeys.revoked";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Info;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>
/// Data payload for an "API key revoked" notification.
/// </summary>
/// <param name="KeyId">Identifier of the revoked key.</param>
public sealed record ApiKeyRevokedNotificationData(
    Guid KeyId);
