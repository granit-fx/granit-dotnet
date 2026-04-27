using Granit.Notifications.Abstractions;
using Granit.Privacy.DataDeletion.Events;
using Granit.Privacy.Regulations;
using Granit.Privacy.Regulations.Profiles;

namespace Granit.Privacy.Notifications.Handlers;

/// <summary>
/// Handles <see cref="PersonalDataDeletionRequestedEto"/> by sending an immediate
/// acknowledgement to the data subject. GDPR Art. 12 §3 obliges the controller to
/// inform the subject of the action taken on their request "without undue delay" —
/// this email is the timestamped paper trail for that obligation.
/// </summary>
/// <remarks>
/// The acknowledgement quotes the statutory response deadline applicable to the
/// data subject's regulation (15 days for LGPD, 30 for GDPR, 45 for CCPA, …).
/// The deadline is resolved from <see cref="IRegulationProfileRegistry"/> using
/// the regulation code carried in the integration event; an unknown or
/// unregistered regulation falls back to GDPR Art. 12 §3's one-month rule.
/// </remarks>
public class DeletionAcknowledgedHandler
{
    /// <summary>GDPR Art. 12 §3 default response deadline, in calendar days.</summary>
    internal const int GdprDefaultDeadlineDays = 30;

    public static async Task HandleAsync(
        PersonalDataDeletionRequestedEto evt,
        INotificationPublisher publisher,
        IRegulationProfileRegistry profileRegistry,
        CancellationToken cancellationToken)
    {
        int deadlineDays = ResolveDeadlineDays(profileRegistry, evt.Regulation);

        await publisher.PublishAsync(
            PrivacyDeletionAcknowledgedNotificationType.Instance,
            new PrivacyDeletionAcknowledgedNotificationData(
                evt.RequestId,
                evt.RequestedAt,
                evt.Regulation,
                deadlineDays),
            [evt.UserId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves the statutory deletion-request deadline (calendar days) for the
    /// supplied regulation code. Returns <see cref="GdprDefaultDeadlineDays"/>
    /// when the code is missing, unparseable, or its profile does not declare a
    /// <c>DeletionRequestDays</c> value — preserving the historical GDPR-anchored
    /// behaviour for hosts that have not configured a regulation profile.
    /// </summary>
    private static int ResolveDeadlineDays(IRegulationProfileRegistry registry, string? regulation)
    {
        if (string.IsNullOrWhiteSpace(regulation))
        {
            return GdprDefaultDeadlineDays;
        }

        // PrivacyRegulation.Create accepts any non-blank string (Tier 1/2/3 codes
        // share the same SingleValueObject<string> shape); the registry returns
        // null for unknown codes and we fall back to GDPR's deadline.
        PrivacyRegulationProfile? profile = registry.GetProfile(PrivacyRegulation.Create(regulation));
        return profile?.DeletionRequestDays ?? GdprDefaultDeadlineDays;
    }
}
