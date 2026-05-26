using Granit.Notifications.Abstractions;
using Granit.Privacy.DataExport.Events;

namespace Granit.Privacy.Notifications.Handlers;

/// <summary>
/// Routes export-lifecycle events to the right user-facing notification:
/// the failure email keys off the saga's <see cref="ExportCompletedEto"/>
/// (the saga is the only place that knows about timeouts / partial results),
/// while the success email keys off <see cref="ExportArchiveAssembledEto"/>
/// (published by the assembly job once every shard is persisted, so the
/// embedded download links can be trusted).
/// </summary>
/// <remarks>
/// Splitting the subscription this way prevents the previous race window in
/// which the "ready" email could fire before any shard reached storage — the
/// user would have followed the link and seen a not-yet-available archive.
/// </remarks>
public class ExportCompletedNotificationHandler
{
    public static async Task HandleAsync(
        ExportCompletedEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        // Only the partial-completion branch fires here — the assembly job emits a
        // dedicated ExportArchiveAssembledEto on success that the second handler
        // below picks up. Complete sagas with no missing providers are intentionally
        // a no-op on this path.
        if (!evt.IsPartial)
        {
            return;
        }

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
    }

    public static async Task HandleAsync(
        ExportArchiveAssembledEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        // Partial completions already fired the failure email from the saga's
        // ExportCompletedEto — skip the success email so the user sees one outcome.
        if (evt.IsPartial)
        {
            return;
        }

        await publisher.PublishAsync(
            PrivacyExportReadyNotificationType.Instance,
            new PrivacyExportReadyNotificationData(
                evt.RequestId,
                evt.ShardCount,
                evt.RequestedAt,
                evt.Regulation),
            [evt.UserId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
