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
    /// Permanently deletes the cached mirror for the given external user (GDPR Art. 17) and
    /// returns the number of rows removed. A <c>null</c> <paramref name="tenantId"/> erases the
    /// mirror across ALL tenant partitions — the right to erasure must not leave a copy behind
    /// in any scope; a non-null value deletes within that tenant only. A no-op (returns 0) if
    /// no entry is cached for that user.
    /// </summary>
    Task<int> EraseAsync(
        string externalUserId,
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}
