using Granit.DataProtection;
using Granit.Encryption;
using Granit.Privacy.DataDeletion.Events;
using Granit.Privacy.DataExport;
using Granit.Privacy.Diagnostics;
using Granit.Privacy.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Granit.Privacy.DataDeletion;

/// <summary>
/// Stateful Saga implementing the privacy deletion cooling-off period plus the provider-deletion
/// fan-in (GDPR Art. 17, LGPD Art. 18, CCPA).
/// </summary>
/// <remarks>
/// <para>
/// Flow:
/// <list type="number">
///   <item><see cref="DeletionDeferredEto"/> starts the Saga and schedules a reminder + deadline.</item>
///   <item>When the reminder fires, the Saga publishes <see cref="DeletionReminderDueEto"/>
///   for the notification bridge to send a reminder email.</item>
///   <item>When the deadline fires, the Saga snapshots the set of registered providers, marks the
///   request <see cref="DeletionRequestState.Executing"/>, publishes
///   <see cref="PersonalDataDeletionRequestedEto"/> (fanning the deletion out to every provider)
///   and <see cref="DeletionExecutedEto"/> (the user-facing confirmation that the grace period
///   ended), then schedules a <see cref="DeletionAcknowledgementTimedOutEvent"/>.</item>
///   <item>Each provider erases its slice and publishes <see cref="PersonalDataDeletedEto"/> as its
///   acknowledgement. The Saga collects these; only once <b>every</b> expected provider has
///   acknowledged does it mark the request <see cref="DeletionRequestState.Executed"/> — the only
///   state that proves Art. 17 completion.</item>
///   <item>If the acknowledgement window elapses first, the Saga marks the request
///   <see cref="DeletionRequestState.PartiallyExecuted"/>, records the missing providers, and emits
///   a stuck-deletion metric + warning log so DLQ monitoring can alert.</item>
///   <item>If the user cancels (<see cref="DeletionCancelledEto"/>), the Saga terminates
///   and future scheduled events are discarded by Wolverine.</item>
/// </list>
/// </para>
/// <para>
/// This mirrors the export saga's scatter-gather fan-in (<c>PersonalDataExportSaga</c>): the
/// expected-provider set is the fan-out target, each provider acknowledgement removes one entry,
/// and a timeout surfaces whatever is still outstanding. The fan-in is idempotent under
/// Wolverine's at-least-once delivery — a duplicate <see cref="PersonalDataDeletedEto"/> is a no-op.
/// </para>
/// <para>
/// Race condition safety: <c>MarkCompleted()</c> guarantees that whichever event arrives after the
/// Saga completes (a late cancel, a straggler acknowledgement, or the acknowledgement timeout after
/// the last ack already completed the Saga) is silently discarded by Wolverine.
/// </para>
/// </remarks>
public sealed partial class PersonalDataDeletionSaga : Saga
{
    /// <summary>Saga correlation ID — equals <see cref="DeletionDeferredEto.RequestId"/>.</summary>
    public Guid Id { get; set; }

    /// <summary>User whose data deletion is deferred.</summary>
    public Guid UserId { get; set; }

    /// <summary>Who requested the deletion (email or identifier).</summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
    [Encrypted]
    public string RequestedBy { get; set; } = string.Empty;

    /// <summary>Reason provided by the user for deletion.</summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
    [Encrypted]
    public string Reason { get; set; } = string.Empty;

    /// <summary>When the deletion was originally requested.</summary>
    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>When the data will be permanently deleted if not cancelled.</summary>
    public DateTimeOffset ScheduledDeletionAt { get; set; }

    /// <summary>Applicable privacy regulation code for this deletion request.</summary>
    public string Regulation { get; set; } = string.Empty;

    /// <summary>Tenant identifier propagated from the starting event for metrics tagging.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Whether the reminder notification has been sent.</summary>
    public bool ReminderSent { get; set; }

    /// <summary>
    /// Providers that still owe an acknowledgement. Snapshotted from
    /// <see cref="IDataProviderRegistry"/> when the deadline is reached; each
    /// <see cref="PersonalDataDeletedEto"/> removes one entry. Empty ⇒ every provider acknowledged.
    /// </summary>
    public List<string> PendingProviders { get; set; } = [];

    /// <summary>Number of providers the fan-out targeted (snapshot of the registry at deadline).</summary>
    public int ExpectedProviderCount { get; set; }

    /// <summary>When the deletion deadline was reached and the provider fan-out began.</summary>
    public DateTimeOffset? DeletionStartedAt { get; set; }

    /// <summary>
    /// Starts the Saga when a user defers their deletion request.
    /// Schedules a reminder notification and the actual deletion deadline.
    /// </summary>
    // NOTE: Named `Start` (no `Async` suffix) so Wolverine's SagaChain discovers it.
    // SagaChain.findByNames is strict-match and does NOT strip `Async`, unlike general
    // handler discovery. An `Async`-suffixed name compiles to a silent no-op handler.
    public async Task Start(
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
        Regulation = @event.Regulation;
        TenantId = @event.TenantId;
        RequestedAt = @event.RequestedAt;
        ScheduledDeletionAt = @event.ScheduledDeletionAt;

        await tracker.RecordDeferredAsync(
            Id, UserId, Reason, RequestedAt, ScheduledDeletionAt).ConfigureAwait(false);

        metrics.RecordDeletionDeferred(TenantId, Regulation);

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
        metrics.RecordDeletionReminderSent(TenantId, Regulation);

        return new DeletionReminderDueEto(Id, UserId, ScheduledDeletionAt);
    }

    /// <summary>
    /// Handles the deadline timeout — begins the provider fan-out by publishing
    /// <see cref="PersonalDataDeletionRequestedEto"/> and the user-facing confirmation
    /// <see cref="DeletionExecutedEto"/>, marks the request
    /// <see cref="DeletionRequestState.Executing"/>, and schedules the acknowledgement timeout.
    /// The request is NOT marked <see cref="DeletionRequestState.Executed"/> here — that only
    /// happens once every provider acknowledges (<see cref="Handle(PersonalDataDeletedEto,
    /// IDeletionRequestTrackerWriter, PrivacyMetrics, TimeProvider)"/>).
    /// </summary>
    /// <remarks>
    /// When no providers are registered the fan-out is empty, so the request is marked executed
    /// immediately and the Saga completes — mirroring the export saga's zero-provider fast path.
    /// The user-facing <see cref="DeletionExecutedEto"/> fires as soon as the grace period ends
    /// (unchanged behaviour): it confirms the deadline was reached, independent of how long the
    /// downstream provider erasure takes.
    /// </remarks>
    // NOTE: Named `Handle` (no `Async` suffix) — see the comment on `Start` above.
    public async Task<object[]> Handle(
        DeletionDeadlineReachedEvent @event,
        IDeletionRequestTrackerWriter tracker,
        IDataProviderRegistry providerRegistry,
        IOptions<GranitPrivacyOptions> options,
        IMessageContext context,
        PrivacyMetrics metrics,
        TimeProvider timeProvider)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DeletionStartedAt = now;

        PendingProviders = [.. providerRegistry.GetAll()];
        ExpectedProviderCount = PendingProviders.Count;

        PersonalDataDeletionRequestedEto deletionRequested =
            new(Id, UserId, RequestedBy, now, Reason, Regulation, TenantId);
        DeletionExecutedEto executed = new(Id, UserId, now);

        if (ExpectedProviderCount == 0)
        {
            // No providers to fan out to — the erasure is trivially complete the moment the
            // deadline is reached (mirrors the export saga's ExpectedCount == 0 fast path).
            await tracker.MarkExecutedAsync(Id, now).ConfigureAwait(false);
            metrics.RecordDeletionExecuted(TenantId, Regulation);
            MarkCompleted();
            return [deletionRequested, executed];
        }

        await tracker.MarkExecutingAsync(Id).ConfigureAwait(false);

        // Schedule the acknowledgement window. If a provider never acknowledges, this timeout
        // resolves the request to PartiallyExecuted rather than leaving it stuck in Executing.
        await context.ScheduleAsync(
            new DeletionAcknowledgementTimedOutEvent(Id),
            TimeSpan.FromMinutes(options.Value.DeletionAcknowledgementTimeoutMinutes)).ConfigureAwait(false);

        return [deletionRequested, executed];
    }

    /// <summary>
    /// Collects a provider's deletion acknowledgement (<see cref="PersonalDataDeletedEto"/>).
    /// When the last expected provider acknowledges, marks the request
    /// <see cref="DeletionRequestState.Executed"/> and completes the Saga. Idempotent: a duplicate
    /// acknowledgement (Wolverine at-least-once redelivery) or an unregistered provider's event is
    /// counted for observability but does not double-complete the Saga.
    /// </summary>
    public async Task Handle(
        PersonalDataDeletedEto @event,
        IDeletionRequestTrackerWriter tracker,
        PrivacyMetrics metrics,
        TimeProvider timeProvider)
    {
        bool expected = PendingProviders.Remove(@event.ProviderName);
        metrics.RecordDeletionAcknowledged(TenantId, expected ? @event.ProviderName : "unknown", Regulation);

        // Still waiting on providers (or this was a duplicate / unexpected ack that drained
        // nothing) — stay active. A duplicate ack after completion cannot reach here because
        // MarkCompleted deletes the saga state.
        if (PendingProviders.Count > 0)
        {
            return;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        await tracker.MarkExecutedAsync(Id, now).ConfigureAwait(false);
        metrics.RecordDeletionExecuted(TenantId, Regulation);
        MarkCompleted();
    }

    /// <summary>
    /// Handles the acknowledgement timeout — at least one provider never acknowledged erasure.
    /// Marks the request <see cref="DeletionRequestState.PartiallyExecuted"/>, records the missing
    /// providers, and emits a stuck-deletion metric + warning log per missing provider so DLQ
    /// monitoring can alert. If every provider had already acknowledged, the Saga is already
    /// completed and Wolverine discards this event before it reaches the handler.
    /// </summary>
    // NOTE: Named `Handle` (no `Async` suffix) — see the comment on `Start` above.
    public async Task Handle(
        DeletionAcknowledgementTimedOutEvent @event,
        IDeletionRequestTrackerWriter tracker,
        PrivacyMetrics metrics,
        ILogger<PersonalDataDeletionSaga> logger,
        TimeProvider timeProvider)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        IReadOnlyList<string> missing = [.. PendingProviders];

        await tracker.MarkPartiallyExecutedAsync(Id, now, missing).ConfigureAwait(false);

        foreach (string provider in missing)
        {
            metrics.RecordDeletionStuck(TenantId, provider, Regulation);
            Log.DeletionProviderStuck(logger, Id, UserId, provider, ExpectedProviderCount - PendingProviders.Count, ExpectedProviderCount);
        }

        MarkCompleted();
    }

    /// <summary>
    /// Handles cancellation — the Saga terminates and future scheduled events
    /// (reminder, deadline) are silently discarded by Wolverine.
    /// </summary>
    // NOTE: Named `Handle` (no `Async` suffix) — see the comment on `Start` above.
    public async Task Handle(
        DeletionCancelledEto @event,
        IDeletionRequestTrackerWriter tracker,
        PrivacyMetrics metrics)
    {
        await tracker.MarkCancelledAsync(Id, @event.CancelledAt).ConfigureAwait(false);
        metrics.RecordDeletionCancelled(TenantId, Regulation);
        MarkCompleted();
    }

    private static partial class Log
    {
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Deletion request {RequestId} (user {UserId}) timed out awaiting provider {Provider} — "
                + "{Acknowledged}/{Expected} providers acknowledged. Request marked PartiallyExecuted; "
                + "reconcile the stuck / dead-lettered provider (GDPR Art. 17 provability).")]
        public static partial void DeletionProviderStuck(
            ILogger logger, Guid requestId, Guid userId, string provider, int acknowledged, int expected);
    }
}
