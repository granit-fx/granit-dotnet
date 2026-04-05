using Granit.Notifications.Abstractions;
using Granit.Privacy.LegalAgreements;
using Granit.Privacy.LegalAgreements.Events;

namespace Granit.Privacy.Notifications.Handlers;

/// <summary>
/// Handles <see cref="LegalAgreementObsoleteEto"/> by sending re-consent notifications
/// to all users who accepted the now-obsolete version.
/// </summary>
/// <remarks>
/// Uses <see cref="ILegalAgreementStoreReader.StreamUsersByDocumentVersionAsync"/> for
/// cursor-based streaming to avoid loading all user IDs into memory at once (OOM protection
/// for large tenants with 500K+ users). Processes in batches of 1000 to avoid saturating
/// the message broker.
/// </remarks>
public class LegalDocumentObsoleteHandler
{
    private const int BatchSize = 1000;

    public static async Task HandleAsync(
        LegalAgreementObsoleteEto evt,
        ILegalAgreementStoreReader storeReader,
        ILegalDocumentRegistry documentRegistry,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        LegalDocumentDefinition? definition = documentRegistry.GetDefinition(evt.DocumentId);
        string displayName = definition?.DisplayName ?? evt.DocumentId;

        var data = new PrivacyLegalDocumentObsoleteNotificationData(
            evt.DocumentId, displayName, evt.OldVersion, evt.NewVersion);

        List<string> batch = new(BatchSize);

        await foreach (Guid userId in storeReader
            .StreamUsersByDocumentVersionAsync(evt.DocumentId, evt.OldVersion, cancellationToken)
            .ConfigureAwait(false))
        {
            batch.Add(userId.ToString());

            if (batch.Count >= BatchSize)
            {
                await publisher.PublishAsync(
                    PrivacyLegalDocumentObsoleteNotificationType.Instance,
                    data,
                    batch,
                    cancellationToken).ConfigureAwait(false);
                batch.Clear();
            }
        }

        // Flush remaining.
        if (batch.Count > 0)
        {
            await publisher.PublishAsync(
                PrivacyLegalDocumentObsoleteNotificationType.Instance,
                data,
                batch,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
