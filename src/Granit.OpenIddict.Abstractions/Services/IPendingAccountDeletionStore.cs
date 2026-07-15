namespace Granit.OpenIddict.Services;

/// <summary>
/// Reads soft-deleted users whose <c>AccountDeletedEto</c> has not yet been dispatched, and marks
/// them dispatched — the durable state the reconciliation job uses to guarantee the GDPR Art. 17
/// erasure event is published exactly-eventually, even if the process crashes between the
/// soft-delete commit and the event publish.
/// </summary>
public interface IPendingAccountDeletionStore
{
    /// <summary>
    /// Returns soft-deleted users with no dispatched erasure event, across every tenant (the sweep
    /// runs in host context, so the multi-tenant filter is bypassed for this read).
    /// </summary>
    /// <param name="max">Maximum number of pending deletions to return in one sweep.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<PendingAccountDeletion>> GetPendingAsync(
        int max, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stamps <c>DeletionEventDispatchedAt</c> on the user so the deletion is not re-published.
    /// </summary>
    /// <param name="userId">The user whose erasure event was dispatched.</param>
    /// <param name="dispatchedAt">The dispatch timestamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkDispatchedAsync(
        Guid userId, DateTimeOffset dispatchedAt, CancellationToken cancellationToken = default);
}

/// <summary>A soft-deleted user awaiting erasure-event dispatch.</summary>
/// <param name="UserId">The deleted user's identifier.</param>
/// <param name="TenantId">The owning tenant, or <see langword="null"/> for a global user.</param>
public sealed record PendingAccountDeletion(Guid UserId, Guid? TenantId);
