using Granit.Identity.Federated.Domain;

namespace Granit.Identity.Federated;

/// <summary>
/// Narrow public read-only view over the federated user cache. Intended for downstream
/// modules that need to look up a user mirror without taking a dependency on the internal
/// <c>IUserCacheStore</c> data-access contract — notably the privacy export provider in
/// <c>Granit.Identity.Federated.Privacy</c>.
/// </summary>
public interface IFederatedUserCacheReader
{
    /// <summary>
    /// Returns the cached mirror for the given external user within a tenant scope,
    /// or <see langword="null"/> if the user has never been cached locally.
    /// </summary>
    Task<UserCacheEntry?> FindByExternalIdAsync(
        string externalUserId,
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}
