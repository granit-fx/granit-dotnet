using Granit.Events;
using Granit.Identity.Local.Events;
using Granit.OpenIddict.Services;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.OpenIddict.Internal;

/// <summary>
/// Publishes the <see cref="AccountDeletedEto"/> for every soft-deleted user whose erasure event has
/// not yet been dispatched, then marks it dispatched.
/// </summary>
/// <remarks>
/// The account-deletion service records the soft-delete durably but does not publish the event
/// inline — the OpenIddict DbContext is not enrolled in the Wolverine outbox, so an inline publish
/// after the commit could be lost to a crash before it reaches the durable outbox. This reconciler
/// (driven by a recurring job) closes that gap: the soft-delete row is the durable intent, and the
/// event is published at-least-once from it. Consumers of <see cref="AccountDeletedEto"/> are already
/// idempotent, so a rare re-publish (crash between publish and mark) is safe.
/// </remarks>
internal sealed partial class AccountDeletionEtoReconciler(
    IPendingAccountDeletionStore store,
    IDistributedEventBus eventBus,
    IClock clock,
    ILogger<AccountDeletionEtoReconciler> logger) : IAccountDeletionEtoReconciler
{
    private const int BatchSize = 500;

    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<PendingAccountDeletion> pending = await store
            .GetPendingAsync(BatchSize, cancellationToken).ConfigureAwait(false);

        if (pending.Count == 0)
        {
            return;
        }

        LogReconciling(logger, pending.Count);

        foreach (PendingAccountDeletion deletion in pending)
        {
            // Publish before marking: if the process dies between the two, the next sweep re-publishes
            // (at-least-once). Marking first would risk dropping the event entirely.
            await eventBus.PublishAsync(
                new AccountDeletedEto(deletion.UserId, deletion.TenantId), cancellationToken)
                .ConfigureAwait(false);

            await store.MarkDispatchedAsync(deletion.UserId, clock.Now, cancellationToken)
                .ConfigureAwait(false);

            LogDispatched(logger, deletion.UserId);
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Reconciling {Count} pending account-deletion erasure event(s).")]
    private static partial void LogReconciling(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Dispatched AccountDeletedEto for user {UserId}.")]
    private static partial void LogDispatched(ILogger logger, Guid userId);
}
