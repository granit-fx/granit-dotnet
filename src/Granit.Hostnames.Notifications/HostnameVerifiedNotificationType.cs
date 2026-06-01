using Granit.Notifications;

namespace Granit.Hostnames.Notifications;

/// <summary>
/// Notification type for a successful hostname DNS verification. Sent to the resource
/// owner when their hostname transitions to <c>Active</c>.
/// </summary>
public sealed class HostnameVerifiedNotificationType
    : NotificationType<HostnameVerifiedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly HostnameVerifiedNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "hostnames.hostname_verified";

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email];
}

/// <summary>
/// Data payload for a hostname-verified notification.
/// </summary>
/// <param name="HostnameId">Identifier of the verified hostname.</param>
/// <param name="Host">The verified fully-qualified domain name.</param>
/// <param name="OwnerType">Owner-resource discriminator (e.g. <c>"cms.site"</c>).</param>
/// <param name="OwnerId">Owning resource identifier.</param>
public sealed record HostnameVerifiedNotificationData(
    Guid HostnameId,
    string Host,
    string OwnerType,
    Guid OwnerId);
