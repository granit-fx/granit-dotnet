namespace Granit.OpenIddict.Services;

/// <summary>
/// Reads accounts that owe a <c>UserRegisteredEto</c> whose event has not yet been dispatched, and
/// marks them dispatched — the durable state the reconciliation job uses to guarantee the
/// registration side effects (default-role assignment, welcome notification) are published
/// eventually, even if the process crashes between the account creation and the inline publish.
/// </summary>
public interface IPendingRegistrationStore
{
    /// <summary>
    /// Returns accounts with a non-null <c>RegistrationEventPendingSince</c>, across every tenant (the
    /// sweep runs in host context, so the multi-tenant filter is bypassed for this read). Soft-deleted
    /// accounts are excluded — a registration event for a since-deleted account is moot.
    /// </summary>
    /// <param name="max">Maximum number of pending registrations to return in one sweep.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<PendingRegistration>> GetPendingAsync(
        int max, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears <c>RegistrationEventPendingSince</c> on the account so the registration is not re-published.
    /// </summary>
    /// <param name="userId">The user whose registration event was dispatched.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkDispatchedAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>An account awaiting registration-event dispatch.</summary>
/// <param name="UserId">The registered user's identifier.</param>
/// <param name="TenantId">The owning tenant, or <see langword="null"/> for a global user.</param>
public sealed record PendingRegistration(Guid UserId, Guid? TenantId);
