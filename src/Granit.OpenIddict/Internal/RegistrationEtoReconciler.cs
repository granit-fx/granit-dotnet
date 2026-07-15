using Granit.Events;
using Granit.Identity.Local.Events;
using Granit.OpenIddict.Services;
using Microsoft.Extensions.Logging;

namespace Granit.OpenIddict.Internal;

/// <summary>
/// Publishes the <see cref="UserRegisteredEto"/> for every account that still owes a registration
/// event, then clears the pending marker.
/// </summary>
/// <remarks>
/// The registration flow publishes the event inline for immediate side effects (default-role
/// assignment, welcome notification) but records the intent durably first — the OpenIddict DbContext
/// is not enrolled in the Wolverine outbox, so an inline publish after the account-creation commit
/// could be lost to a crash before it reaches the durable outbox. This reconciler (driven by a
/// recurring job) closes that gap: the <c>RegistrationEventPendingSince</c> marker is the durable
/// intent, and the event is published at-least-once from it. Consumers of
/// <see cref="UserRegisteredEto"/> are idempotent, so a rare re-publish (crash between the inline
/// publish and the clear) is safe.
/// </remarks>
internal sealed partial class RegistrationEtoReconciler(
    IPendingRegistrationStore store,
    IDistributedEventBus eventBus,
    ILogger<RegistrationEtoReconciler> logger) : IRegistrationEtoReconciler
{
    private const int BatchSize = 500;

    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<PendingRegistration> pending = await store
            .GetPendingAsync(BatchSize, cancellationToken).ConfigureAwait(false);

        if (pending.Count == 0)
        {
            return;
        }

        LogReconciling(logger, pending.Count);

        foreach (PendingRegistration registration in pending)
        {
            // Publish before marking: if the process dies between the two, the next sweep re-publishes
            // (at-least-once). Marking first would risk dropping the event entirely.
            await eventBus.PublishAsync(
                new UserRegisteredEto(registration.UserId, registration.TenantId), cancellationToken)
                .ConfigureAwait(false);

            await store.MarkDispatchedAsync(registration.UserId, cancellationToken)
                .ConfigureAwait(false);

            LogDispatched(logger, registration.UserId);
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Reconciling {Count} pending registration event(s).")]
    private static partial void LogReconciling(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Dispatched UserRegisteredEto for user {UserId}.")]
    private static partial void LogDispatched(ILogger logger, Guid userId);
}
