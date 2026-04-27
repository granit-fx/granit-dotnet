using Granit.Notifications.Abstractions;
using Granit.Privacy.DataExport.Events;

namespace Granit.Privacy.Notifications.Handlers;

/// <summary>
/// Handles <see cref="ExportCompletedEto"/> by routing to the right user-facing
/// notification depending on whether the saga produced a complete or partial archive.
/// </summary>
/// <remarks>
/// Branching at the handler level (rather than registering two competing handlers on
/// the same event) avoids having both the "ready" and "failed" notifications
/// dispatched for a single completion — the user sees exactly one outcome email.
/// </remarks>
public class ExportCompletedNotificationHandler
{
    public static async Task HandleAsync(
        ExportCompletedEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        if (evt.IsPartial)
        {
            await publisher.PublishAsync(
                PrivacyExportFailedNotificationType.Instance,
                new PrivacyExportFailedNotificationData(
                    evt.RequestId,
                    evt.ArchiveBlobReferenceId,
                    evt.MissingProviders,
                    string.Join(", ", evt.MissingProviders),
                    evt.RequestedAt,
                    evt.Regulation),
                [evt.UserId.ToString()],
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await publisher.PublishAsync(
            PrivacyExportReadyNotificationType.Instance,
            new PrivacyExportReadyNotificationData(
                evt.RequestId,
                evt.ArchiveBlobReferenceId,
                evt.RequestedAt,
                evt.Regulation),
            [evt.UserId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
