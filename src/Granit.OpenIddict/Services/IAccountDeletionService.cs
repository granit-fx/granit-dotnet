namespace Granit.OpenIddict.Services;

/// <summary>
/// Handles GDPR account deletion (right to erasure, Article 17).
/// </summary>
/// <remarks>
/// Performs a soft-delete, revokes all active tokens, and publishes
/// <see cref="Events.AccountDeletedEto"/> via <c>IDistributedEventBus</c>.
/// </remarks>
public interface IAccountDeletionService
{
    /// <summary>
    /// Initiates account deletion for the specified user.
    /// </summary>
    /// <param name="userId">The user identifier to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InitiateAsync(string userId, CancellationToken cancellationToken = default);
}
