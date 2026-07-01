using Granit.Notifications;

namespace Granit.Hostnames.Notifications;

/// <summary>
/// Notification type for a failed SSL/TLS certificate provisioning attempt. Sent to the
/// resource owner when the edge provider reports the certificate for their hostname as
/// errored (including expiry of a previously active certificate).
/// </summary>
public sealed class HostnamesCertificateFailedNotificationType
    : NotificationType<HostnamesCertificateFailedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly HostnamesCertificateFailedNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "hostnames.certificate_failed";

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email];
}

/// <summary>
/// Data payload for a certificate-failed notification.
/// </summary>
/// <param name="HostnameId">Identifier of the affected hostname.</param>
/// <param name="Host">The hostname value (FQDN).</param>
/// <param name="OwnerType">Owner-resource discriminator.</param>
/// <param name="OwnerId">Owning resource identifier.</param>
public sealed record HostnamesCertificateFailedNotificationData(
    Guid HostnameId,
    string Host,
    string OwnerType,
    Guid OwnerId);
