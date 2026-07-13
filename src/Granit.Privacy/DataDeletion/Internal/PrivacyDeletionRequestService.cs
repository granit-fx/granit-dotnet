using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Privacy.DataDeletion.Events;
using Granit.Privacy.Diagnostics;
using Granit.Privacy.Options;
using Microsoft.Extensions.Options;

namespace Granit.Privacy.DataDeletion.Internal;

/// <summary>
/// Default <see cref="IPrivacyDeletionRequestService"/> orchestrating the deletion workflow.
/// The trackers are optional: a host that maps the deletion endpoints wires them via
/// <c>UseDeletionRequestTracker</c>, but a host that only references the module must still be
/// able to construct this service, so the reader/writer default to <c>null</c> and the
/// duplicate guard / immediate-record steps degrade gracefully when absent.
/// </summary>
internal sealed class PrivacyDeletionRequestService(
    IDistributedEventBus eventBus,
    PrivacyMetrics metrics,
    TimeProvider timeProvider,
    ICurrentTenant currentTenant,
    IOptions<GranitPrivacyOptions> options,
    IGuidGenerator guidGenerator,
    IDeletionRequestTrackerReader? deletionTracker = null,
    IDeletionRequestTrackerWriter? deletionTrackerWriter = null) : IPrivacyDeletionRequestService
{
    public async Task<RequestDeletionOutcome> RequestDeletionAsync(
        RequestDeletionCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (deletionTracker is not null)
        {
            IReadOnlyList<DeletionRequestStatus> existing = await deletionTracker
                .GetByUserAsync(command.UserId, cancellationToken)
                .ConfigureAwait(false);

            if (existing.Any(r => r.State == DeletionRequestState.Deferred))
            {
                return new RequestDeletionOutcome(RequestDeletionResult.DuplicatePending, Guid.Empty, null);
            }
        }

        Guid requestId = guidGenerator.Create();
        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        if (command.Defer)
        {
            int graceDays = options.Value.DefaultGracePeriodDays;
            DateTimeOffset scheduledDeletionAt = now.AddDays(graceDays);

            await eventBus
                .PublishAsync(
                    new DeletionDeferredEto(requestId, command.UserId, command.RequestedBy, now, command.Reason, scheduledDeletionAt, command.Regulation, tenantId),
                    cancellationToken)
                .ConfigureAwait(false);

            metrics.RecordDeletionDeferred(tenantId, command.Regulation);

            return new RequestDeletionOutcome(RequestDeletionResult.Deferred, requestId, scheduledDeletionAt);
        }

        // Immediate deletion + confirmation event + audit trail (GDPR Art. 5(2)).
        if (deletionTrackerWriter is not null)
        {
            await deletionTrackerWriter
                .RecordImmediateDeletionAsync(requestId, command.UserId, command.Reason, now, cancellationToken)
                .ConfigureAwait(false);
        }

        await eventBus
            .PublishAsync(
                new PersonalDataDeletionRequestedEto(requestId, command.UserId, command.RequestedBy, now, command.Reason, command.Regulation, tenantId),
                cancellationToken)
            .ConfigureAwait(false);

        await eventBus
            .PublishAsync(
                new DeletionExecutedEto(requestId, command.UserId, now),
                cancellationToken)
            .ConfigureAwait(false);

        metrics.RecordDeletionRequested(tenantId, command.Regulation);
        metrics.RecordDeletionExecuted(tenantId, command.Regulation);

        return new RequestDeletionOutcome(RequestDeletionResult.ExecutedImmediately, requestId, null);
    }

    public async Task<CancelDeletionOutcome> CancelDeletionAsync(
        Guid requestId, Guid userId, CancellationToken cancellationToken = default)
    {
        DeletionRequestStatus? status = deletionTracker is null
            ? null
            : await deletionTracker.GetStatusAsync(requestId, cancellationToken).ConfigureAwait(false);

        // Unknown request, or one owned by a different user — same 404 shape either way so a
        // caller cannot probe for other users' request ids.
        if (status is null || status.UserId != userId)
        {
            return new CancelDeletionOutcome(CancelDeletionResult.NotFound);
        }

        if (status.State != DeletionRequestState.Deferred)
        {
            return new CancelDeletionOutcome(CancelDeletionResult.NotCancellable, status.State);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        await eventBus
            .PublishAsync(new DeletionCancelledEto(requestId, userId, now), cancellationToken)
            .ConfigureAwait(false);

        return new CancelDeletionOutcome(CancelDeletionResult.Cancelled);
    }
}
