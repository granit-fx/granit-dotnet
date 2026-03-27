using Granit.DataProtection;
using Granit.Privacy.DataDeletion.Events;
using Granit.Privacy.Diagnostics;
using Granit.Privacy.Options;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Granit.Privacy.DataDeletion;

/// <summary>
/// Stateful Saga implementing the GDPR deletion cooling-off period (RGPD Art. 17).
/// </summary>
/// <remarks>
/// <para>
/// Flow:
/// <list type="number">
///   <item><see cref="DeletionDeferredEto"/> starts the Saga and schedules a reminder + deadline.</item>
///   <item>When the reminder fires, the Saga publishes <see cref="DeletionReminderDueEto"/>
///   for the notification bridge to send a reminder email.</item>
///   <item>When the deadline fires, the Saga publishes <see cref="PersonalDataDeletionRequestedEto"/>
///   (triggering actual deletion by providers) and <see cref="DeletionExecutedEto"/>
///   (triggering a confirmation email).</item>
///   <item>If the user cancels (<see cref="DeletionCancelledEto"/>), the Saga terminates
///   and future scheduled events are discarded by Wolverine.</item>
/// </list>
/// </para>
/// <para>
/// Race condition safety: <c>MarkCompleted()</c> guarantees that whichever event arrives
/// second (cancel vs deadline) is silently discarded.
/// </para>
/// </remarks>
public sealed class GdprDeletionSaga : Saga
{
    /// <summary>Saga correlation ID — equals <see cref="DeletionDeferredEto.RequestId"/>.</summary>
    public Guid Id { get; set; }

    /// <summary>User whose data deletion is deferred.</summary>
    public Guid UserId { get; set; }

    /// <summary>Who requested the deletion (email or identifier).</summary>
    public string RequestedBy { get; set; } = string.Empty;

    /// <summary>Reason provided by the user for deletion.</summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>When the deletion was originally requested.</summary>
    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>When the data will be permanently deleted if not cancelled.</summary>
    public DateTimeOffset ScheduledDeletionAt { get; set; }

    /// <summary>Whether the reminder notification has been sent.</summary>
    public bool ReminderSent { get; set; }

    /// <summary>
    /// Starts the Saga when a user defers their deletion request.
    /// Schedules a reminder notification and the actual deletion deadline.
    /// </summary>
    public async Task StartAsync(
        DeletionDeferredEto @event,
        IOptions<GranitPrivacyOptions> options,
        IMessageContext context,
        IDeletionRequestTrackerWriter tracker,
        PrivacyMetrics metrics)
    {
        Id = @event.RequestId;
        UserId = @event.UserId;
        RequestedBy = @event.RequestedBy;
        Reason = @event.Reason;
        RequestedAt = @event.RequestedAt;
        ScheduledDeletionAt = @event.ScheduledDeletionAt;

        await tracker.RecordDeferredAsync(
            Id, UserId, Reason, RequestedAt, ScheduledDeletionAt).ConfigureAwait(false);

        metrics.RecordDeletionDeferred(null);

        TimeSpan gracePeriod = ScheduledDeletionAt - RequestedAt;
        int reminderDaysBefore = options.Value.ReminderDaysBefore;

        // Schedule reminder only if grace period is longer than reminder lead time
        if (reminderDaysBefore > 0 && gracePeriod.TotalDays > reminderDaysBefore)
        {
            TimeSpan reminderDelay = gracePeriod - TimeSpan.FromDays(reminderDaysBefore);
            await context.ScheduleAsync(
                new DeletionReminderDueEvent(Id),
                reminderDelay).ConfigureAwait(false);
        }

        // Schedule the actual deletion
        await context.ScheduleAsync(
            new DeletionDeadlineReachedEvent(Id),
            gracePeriod).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles the reminder timeout — publishes <see cref="DeletionReminderDueEto"/>
    /// for the notification bridge. The Saga stays active.
    /// </summary>
    public DeletionReminderDueEto Handle(DeletionReminderDueEvent @event, PrivacyMetrics metrics)
    {
        ReminderSent = true;
        metrics.RecordDeletionReminderSent(null);

        return new DeletionReminderDueEto(Id, UserId, ScheduledDeletionAt);
    }

    /// <summary>
    /// Handles the deadline timeout — triggers actual deletion by publishing
    /// <see cref="PersonalDataDeletionRequestedEto"/> and sends a confirmation
    /// via <see cref="DeletionExecutedEto"/>.
    /// </summary>
    public async Task<object[]> HandleAsync(
        DeletionDeadlineReachedEvent @event,
        IDeletionRequestTrackerWriter tracker,
        PrivacyMetrics metrics,
        TimeProvider timeProvider)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();

        await tracker.MarkExecutedAsync(Id, now).ConfigureAwait(false);
        metrics.RecordDeletionExecuted(null);
        MarkCompleted();

        return
        [
            new PersonalDataDeletionRequestedEto(Id, UserId, RequestedBy, now, Reason),
            new DeletionExecutedEto(Id, UserId, now),
        ];
    }

    /// <summary>
    /// Handles cancellation — the Saga terminates and future scheduled events
    /// (reminder, deadline) are silently discarded by Wolverine.
    /// </summary>
    public async Task HandleAsync(
        DeletionCancelledEto @event,
        IDeletionRequestTrackerWriter tracker,
        PrivacyMetrics metrics)
    {
        await tracker.MarkCancelledAsync(Id, @event.CancelledAt).ConfigureAwait(false);
        metrics.RecordDeletionCancelled(null);
        MarkCompleted();
    }
}
