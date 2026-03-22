using Granit.Core.Events;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;
using Granit.Privacy.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Granit.Privacy.Jobs;

/// <summary>
/// Handles <see cref="DeletionDeadlineEnforcerJob"/> by scanning for expired deferred
/// deletion requests and forcing execution. Idempotent — skips requests already in
/// <see cref="DeletionRequestState.Executed"/> or <see cref="DeletionRequestState.Cancelled"/> state.
/// </summary>
internal static partial class DeletionDeadlineEnforcerHandler
{
    public static async Task HandleAsync(
        DeletionDeadlineEnforcerJob job,
        IDeletionRequestTrackerReader trackerReader,
        IDeletionRequestTrackerWriter trackerWriter,
        IDistributedEventBus eventBus,
        TimeProvider timeProvider,
        PrivacyMetrics metrics,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();

        IReadOnlyList<DeletionRequestStatus> expired =
            await trackerReader.GetExpiredDeferredAsync(now, cancellationToken).ConfigureAwait(false);

        if (expired.Count == 0)
        {
            return;
        }

        LogExpiredRequestsFound(logger, expired.Count);

        foreach (DeletionRequestStatus request in expired)
        {
            await trackerWriter.MarkExecutedAsync(request.RequestId, now, cancellationToken).ConfigureAwait(false);

            await eventBus.PublishAsync(
                new PersonalDataDeletionRequestedEto(
                    request.RequestId,
                    request.UserId,
                    "system:deadline-enforcer",
                    now,
                    request.Reason),
                cancellationToken).ConfigureAwait(false);

            await eventBus.PublishAsync(
                new DeletionExecutedEto(request.RequestId, request.UserId, now),
                cancellationToken).ConfigureAwait(false);

            metrics.RecordDeletionExecuted(null);
            LogDeletionEnforced(logger, request.RequestId, request.UserId);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deletion deadline enforcer found {Count} expired deferred request(s)")]
    private static partial void LogExpiredRequestsFound(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Enforced deletion for request {RequestId}, user {UserId}")]
    private static partial void LogDeletionEnforced(ILogger logger, Guid requestId, Guid userId);
}
