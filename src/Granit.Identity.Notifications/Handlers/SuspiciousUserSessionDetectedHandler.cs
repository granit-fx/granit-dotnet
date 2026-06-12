using Granit.Identity.Notifications.Internal;
using Granit.Notifications.Abstractions;

namespace Granit.Identity.Notifications.Handlers;

/// <summary>
/// Handles <see cref="SuspiciousUserSessionDetectedEto"/> by sending the account owner a localized
/// security alert email. Routes by risk level:
/// <list type="bullet">
/// <item><c>High</c> → the hard-locked <see cref="SuspiciousUserSessionNotificationType"/> (alert tone).</item>
/// <item>anything below <c>High</c> (e.g. <c>Medium</c>) → the opt-out-able
/// <see cref="NewUserSessionReviewNotificationType"/> (reassuring tone).</item>
/// </list>
/// </summary>
/// <remarks>
/// Tenant scope is established by the distributed-dispatch framework before this handler runs
/// (from the Eto's tenant), so no manual tenant change is needed — same as the Privacy handlers.
/// Recipient contact (email, culture, display name) is resolved host-side via
/// <c>IRecipientResolver</c>; an unresolved recipient is a graceful no-op in the pipeline.
/// </remarks>
public class SuspiciousUserSessionDetectedHandler
{
    public static async Task HandleAsync(
        SuspiciousUserSessionDetectedEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        // No subject → nothing to alert. Guard before touching the publisher.
        if (string.IsNullOrEmpty(evt.UserId))
        {
            return;
        }

        string reason = evt.Reasons.Count > 0 ? evt.Reasons[0] : "anomaly";
        var data = new SuspiciousUserSessionNotificationData(
            reason,
            string.Join(", ", evt.Reasons),
            evt.City,
            evt.CountryCode,
            evt.IpAddress,
            UserAgentDescriptor.Browser(evt.UserAgent),
            UserAgentDescriptor.OperatingSystem(evt.UserAgent),
            evt.DetectedAt);

        string[] recipients = [evt.UserId];

        if (evt.Level == UserSessionRiskLevel.High)
        {
            await publisher.PublishAsync(
                SuspiciousUserSessionNotificationType.Instance,
                data,
                recipients,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await publisher.PublishAsync(
                NewUserSessionReviewNotificationType.Instance,
                data,
                recipients,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
