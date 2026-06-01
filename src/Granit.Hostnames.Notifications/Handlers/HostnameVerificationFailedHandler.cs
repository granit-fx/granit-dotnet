using Granit.Hostnames.Domain;
using Granit.Hostnames.Domain.Events;
using Granit.Notifications.Abstractions;

namespace Granit.Hostnames.Notifications.Handlers;

/// <summary>
/// Handles <see cref="HostnameVerificationFailedEto"/> by sending an alert notification
/// to the resource owner with a description of the detected DNS conflicts.
/// Published via the Wolverine distributed event bus.
/// </summary>
public class HostnameVerificationFailedHandler
{
    public static async Task HandleAsync(
        HostnameVerificationFailedEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        string conflictsDisplay = BuildConflictsDisplay(evt.Conflicts);

        await publisher.PublishAsync(
            HostnameVerificationFailedNotificationType.Instance,
            new HostnameVerificationFailedNotificationData(
                evt.HostnameId,
                evt.Host,
                evt.OwnerType,
                evt.OwnerId,
                evt.FailedCheckCount,
                evt.Conflicts,
                conflictsDisplay,
                evt.NextCheckAt),
            [evt.OwnerId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }

    private static string BuildConflictsDisplay(IReadOnlyList<DnsConflict> conflicts) =>
        conflicts.Count == 0
            ? string.Empty
            : string.Join("\n", conflicts.Select(c => $"• {c.Details}"));
}
