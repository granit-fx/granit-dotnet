using Granit.Events;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;
using Granit.Privacy.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Granit.Privacy.BackgroundJobs.Internal;

/// <summary>
/// Scans for expired deferred deletion requests and forces execution.
/// Idempotent — skips requests already in
/// <see cref="DeletionRequestState.Executed"/> or <see cref="DeletionRequestState.Cancelled"/> state.
/// </summary>
internal sealed partial class DeletionDeadlineEnforcementService(
    IDeletionRequestTrackerReader trackerReader,
    IDeletionRequestTrackerWriter trackerWriter,
    IDistributedEventBus eventBus,
    TimeProvider timeProvider,
    PrivacyMetrics metrics,
    ILogger<DeletionDeadlineEnforcementService> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();

        IReadOnlyList<DeletionRequestStatus> expired =
            await trackerReader.GetExpiredDeferredAsync(now, cancellationToken).ConfigureAwait(false);

        if (expired.Count == 0)
        {
            return;
        }

        Log.ExpiredRequestsFound(logger, expired.Count);

        foreach (DeletionRequestStatus request in expired)
        {
            // Publish events BEFORE marking executed. Events go into the Wolverine outbox
            // (not committed yet). If MarkExecutedAsync fails, the exception propagates and
            // the outbox is not committed — both operations roll back cleanly on retry.
            // This ordering prevents a GDPR compliance gap where the request is marked
            // "Executed" but the deletion event was never enqueued.
            string regulation = request.Regulation ?? "EU_GDPR";

            await eventBus.PublishAsync(
                new PersonalDataDeletionRequestedEto(
                    request.RequestId,
                    request.UserId,
                    "system:deadline-enforcer",
                    now,
                    request.Reason,
                    regulation,
                    request.TenantId),
                cancellationToken).ConfigureAwait(false);

            await eventBus.PublishAsync(
                new DeletionExecutedEto(request.RequestId, request.UserId, now),
                cancellationToken).ConfigureAwait(false);

            await trackerWriter.MarkExecutedAsync(request.RequestId, now, cancellationToken).ConfigureAwait(false);

            metrics.RecordDeletionExecuted(request.TenantId, regulation);
            Log.DeletionEnforced(logger, request.RequestId, request.UserId);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Deletion deadline enforcer found {Count} expired deferred request(s)")]
        public static partial void ExpiredRequestsFound(ILogger logger, int count);

        [LoggerMessage(Level = LogLevel.Information, Message = "Enforced deletion for request {RequestId}, user {UserId}")]
        public static partial void DeletionEnforced(ILogger logger, Guid requestId, Guid userId);
    }
}
