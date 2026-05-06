using Granit.Activities.Domain;
using Granit.Activities.Events;
using Granit.Activities.Persistence;
using Granit.Events;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.Activities.BackgroundJobs.Services;

/// <summary>
/// Scans open activities due within the day-after-tomorrow's window
/// (<c>[start_of_day_T+1, start_of_day_T+2)</c>) and emits one
/// <see cref="ActivityReminderDueEvent"/> per match.
/// </summary>
/// <remarks>
/// Single-fire per (activity, day) by virtue of the cron cadence (daily) and
/// the day-window query — the scan would only re-emit if an activity was
/// rescheduled into the same window after the previous run, which is the
/// expected behaviour (the user gets a fresh reminder for the new due date).
/// </remarks>
public sealed partial class SendRemindersScanService(
    IActivityReader reader,
    ILocalEventBus eventBus,
    IClock clock,
    ILogger<SendRemindersScanService> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.Normalize(clock.Now);
        DateTimeOffset tomorrowStart = new(now.Date.AddDays(1), TimeSpan.Zero);
        DateTimeOffset dayAfterStart = tomorrowStart.AddDays(1);

        IReadOnlyList<Activity> dueRows = await reader
            .GetOpenDueWithinAsync(tomorrowStart, dayAfterStart, cancellationToken)
            .ConfigureAwait(false);

        if (dueRows.Count == 0)
        {
            return;
        }

        Log.RemindersFound(logger, dueRows.Count);

        foreach (Activity activity in dueRows)
        {
            await eventBus.PublishAsync(new ActivityReminderDueEvent(
                ActivityId: activity.Id,
                Type: activity.Type,
                AssignedToUserId: activity.AssignedToUserId,
                DueAt: activity.DueAt,
                EntityType: activity.EntityType,
                EntityId: activity.EntityId,
                TenantId: activity.TenantId), cancellationToken).ConfigureAwait(false);

            Log.ReminderPublished(logger, activity.Id, activity.AssignedToUserId, activity.DueAt);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information,
            Message = "Activities reminder scanner found {Count} activity(ies) due tomorrow")]
        public static partial void RemindersFound(ILogger logger, int count);

        [LoggerMessage(Level = LogLevel.Information,
            Message = "Published ActivityReminderDueEvent for activity {ActivityId} (assignee {AssigneeId}, due {DueAt:O})")]
        public static partial void ReminderPublished(
            ILogger logger,
            Guid activityId,
            Guid assigneeId,
            DateTimeOffset dueAt);
    }
}
