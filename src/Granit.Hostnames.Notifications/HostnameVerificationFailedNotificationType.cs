using Granit.Hostnames.Domain;
using Granit.Notifications;

namespace Granit.Hostnames.Notifications;

/// <summary>
/// Notification type for a hostname DNS verification failure. Sent to the resource
/// owner when the DNS check finds mismatches or cannot resolve the expected records.
/// </summary>
public sealed class HostnameVerificationFailedNotificationType
    : NotificationType<HostnameVerificationFailedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly HostnameVerificationFailedNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "hostnames.hostname_verification_failed";

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email];
}

/// <summary>
/// Data payload for a hostname-verification-failed notification.
/// </summary>
/// <param name="HostnameId">Identifier of the hostname that failed verification.</param>
/// <param name="Host">The fully-qualified domain name.</param>
/// <param name="OwnerType">Owner-resource discriminator.</param>
/// <param name="OwnerId">Owning resource identifier.</param>
/// <param name="FailedCheckCount">Total consecutive DNS check failures including this one.</param>
/// <param name="Conflicts">Detected DNS conflicts (raw list — use <see cref="ConflictsDisplay"/> in templates).</param>
/// <param name="ConflictsDisplay">
/// Human-readable conflict summary, one line per conflict. Use this in Scriban templates
/// because JSON arrays do not iterate after <c>JsonElementToDictionary</c> flattening.
/// </param>
/// <param name="NextCheckAt">When the poller will retry; <c>null</c> when the hostname is dormant.</param>
public sealed record HostnameVerificationFailedNotificationData(
    Guid HostnameId,
    string Host,
    string OwnerType,
    Guid OwnerId,
    int FailedCheckCount,
    IReadOnlyList<DnsConflict> Conflicts,
    string ConflictsDisplay,
    DateTimeOffset? NextCheckAt);
