using Granit.Notifications;

namespace Granit.Hostnames.Notifications;

/// <summary>
/// Notification type for a successful SSL/TLS certificate issuance. Sent to the resource
/// owner when the edge provider reports the certificate for their hostname as secured.
/// </summary>
public sealed class HostnamesCertificateSecuredNotificationType
    : NotificationType<HostnamesCertificateSecuredNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly HostnamesCertificateSecuredNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "hostnames.certificate_secured";

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email];
}

/// <summary>
/// Data payload for a certificate-secured notification.
/// </summary>
/// <param name="HostnameId">Identifier of the hostname whose certificate was secured.</param>
/// <param name="Host">The hostname value (FQDN).</param>
/// <param name="OwnerType">Owner-resource discriminator (e.g. <c>"cms.site"</c>).</param>
/// <param name="OwnerId">Owning resource identifier.</param>
/// <param name="ExpiresAt">When the issued certificate expires; <c>null</c> when unknown.</param>
public sealed record HostnamesCertificateSecuredNotificationData(
    Guid HostnameId,
    string Host,
    string OwnerType,
    Guid OwnerId,
    DateTimeOffset? ExpiresAt);
