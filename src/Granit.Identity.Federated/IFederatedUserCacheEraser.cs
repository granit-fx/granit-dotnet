namespace Granit.Identity.Federated;

/// <summary>
/// Narrow public write view over the federated user cache, exposing only the erasure
/// operation. Intended for downstream modules that need to purge a cached user mirror
/// without taking a dependency on the internal <c>IUserCacheStore</c> data-access contract —
/// notably the privacy deletion handler in <c>Granit.Identity.Federated.Privacy</c>.
/// </summary>
public interface IFederatedUserCacheEraser
{
    /// <summary>
    /// Permanently deletes the cached mirror for the given external user within a tenant
    /// scope (GDPR Art. 17). A no-op if no entry is cached for that user.
    /// </summary>
    Task EraseAsync(
        string externalUserId,
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}
