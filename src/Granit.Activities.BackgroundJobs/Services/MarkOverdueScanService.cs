using Granit.Activities.Abstractions;
using Granit.Activities.Domain;
using Granit.Activities.Events;
using Granit.Events;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.Activities.BackgroundJobs.Services;

/// <summary>
/// Scans open activities past their due date that have not yet been notified
/// and emits one <see cref="ActivityOverdueEvent"/> per match. Idempotency
/// comes from the <see cref="Activity.OverdueNotifiedAt"/> stamp set after
/// each emission — the next scanner run skips the row.
/// </summary>
/// <remarks>
/// Grace period: a 1-hour buffer is applied so that activities completed
/// within the same poll cycle as their due date do not race the scanner.
/// </remarks>
public sealed partial class MarkOverdueScanService(
    IActivityReader reader,
    IActivityWriter writer,
    ILocalEventBus eventBus,
    IClock clock,
    ILogger<MarkOverdueScanService> logger)
{
    /// <summary>1-hour grace before marking an activity overdue, so completion within the same poll cycle as the due date is not raced.</summary>
    private static readonly TimeSpan GracePeriod = TimeSpan.FromHours(1);

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.Normalize(clock.Now);
        DateTimeOffset cutoff = now - GracePeriod;

        IReadOnlyList<Activity> overdueRows = await reader
            .GetOverdueAwaitingNotificationAsync(cutoff, cancellationToken)
            .ConfigureAwait(false);

        if (overdueRows.Count == 0)
        {
            return;
        }

        Log.OverdueActivitiesFound(logger, overdueRows.Count);

        foreach (Activity activity in overdueRows)
        {
            int overdueByDays = Math.Max(1, (int)Math.Ceiling((now - activity.DueAt).TotalDays));

            // Publish first, then stamp. If publication fails the stamp is not
            // applied and the next scanner run will re-attempt — preserving
            // at-least-once semantics for the assignee notification. If both
            // succeed, OverdueNotifiedAt prevents re-fires for this row.
            await eventBus.PublishAsync(new ActivityOverdueEvent(
                ActivityId: activity.Id,
                Type: activity.Type,
                AssignedToUserId: activity.AssignedToUserId,
                DueAt: activity.DueAt,
                OverdueByDays: overdueByDays,
                EntityType: activity.EntityType,
                EntityId: activity.EntityId,
                TenantId: activity.TenantId), cancellationToken).ConfigureAwait(false);

            await writer.MarkOverdueNotifiedAsync(activity.Id, now, cancellationToken).ConfigureAwait(false);

            Log.OverduePublished(logger, activity.Id, activity.AssignedToUserId, overdueByDays);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information,
            Message = "Activities overdue scanner found {Count} activity(ies) awaiting overdue notification")]
        public static partial void OverdueActivitiesFound(ILogger logger, int count);

        [LoggerMessage(Level = LogLevel.Information,
            Message = "Published ActivityOverdueEvent for activity {ActivityId} (assignee {AssigneeId}, {OverdueByDays} day(s) overdue)")]
        public static partial void OverduePublished(
            ILogger logger,
            Guid activityId,
            Guid assigneeId,
            int overdueByDays);
    }
}
