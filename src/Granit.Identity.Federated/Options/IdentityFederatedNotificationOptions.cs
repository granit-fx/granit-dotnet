using System.ComponentModel.DataAnnotations;

namespace Granit.Identity.Federated.Options;

/// <summary>
/// Configuration options for federated-identity audit notifications, currently
/// covering the <c>IdentityUserSyncFailedEto</c> emission cadence.
/// Bind to the <c>Identity:Federated:Notifications</c> configuration section.
/// </summary>
public sealed class IdentityFederatedNotificationOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Identity:Federated:Notifications";

    /// <summary>
    /// Cool-off window, in minutes, used to rate-limit
    /// <c>IdentityUserSyncFailedEto</c> emissions per
    /// (<c>UserId</c>, <c>ProviderName</c>). One emission is allowed per key
    /// per window — subsequent failures still produce the existing log line
    /// but do not re-emit the integration event. Default: <c>60</c>.
    /// </summary>
    [Range(1, 24 * 60)]
    public int SyncFailureCoolOffMinutes { get; set; } = 60;
}
