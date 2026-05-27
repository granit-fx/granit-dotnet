using Granit.Events;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;
using Granit.Privacy.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Granit.Privacy.BackgroundJobs.Services;

/// <summary>
/// Scans for expired deferred deletion requests and forces execution.
/// Idempotent — skips requests already in
/// <see cref="DeletionRequestState.Executed"/> or <see cref="DeletionRequestState.Cancelled"/> state.
/// </summary>
public sealed partial class DeletionDeadlineEnforcementService(
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

            // Surface the slip between scheduled deadline and actual execution so
            // operators can alert when the daily enforcer misses its window
            // (GDPR Art. 17 "without undue delay"). Slip > a tenant-specific
            // threshold also triggers a warning log so an investigator has a
            // structured artefact next to the metric.
            TimeSpan slip = now - request.ScheduledDeletionAt;
            metrics.RecordDeletionDeadlineSlip(request.TenantId, slip, regulation);
            if (slip > SignificantSlipThreshold)
            {
                Log.DeadlineSlipDetected(logger, request.RequestId, request.UserId, (long)slip.TotalSeconds);
            }

            Log.DeletionEnforced(logger, request.RequestId, request.UserId);
        }
    }

    /// <summary>
    /// Slip threshold above which the enforcer logs a warning beside the
    /// histogram. One hour is a pragmatic default — the job runs daily, so any
    /// slip beyond an hour means the day's window was missed by a noticeable
    /// fraction.
    /// </summary>
    private static readonly TimeSpan SignificantSlipThreshold = TimeSpan.FromHours(1);

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Deletion deadline enforcer found {Count} expired deferred request(s)")]
        public static partial void ExpiredRequestsFound(ILogger logger, int count);

        [LoggerMessage(Level = LogLevel.Information, Message = "Enforced deletion for request {RequestId}, user {UserId}")]
        public static partial void DeletionEnforced(ILogger logger, Guid requestId, Guid userId);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Deletion deadline missed by {SlipSeconds}s for request {RequestId}, user {UserId} (GDPR Art. 17 — investigate enforcer job health)")]
        public static partial void DeadlineSlipDetected(ILogger logger, Guid requestId, Guid userId, long slipSeconds);
    }
}
