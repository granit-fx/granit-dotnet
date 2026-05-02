using Granit.Identity.Federated.Domain;

namespace Granit.Identity.Federated.Internal;

/// <summary>
/// Adapter that exposes a minimal <see cref="IFederatedUserCacheReader"/> view over the
/// internal <see cref="IUserCacheStore"/>. Lets downstream modules (notably the privacy
/// export provider) look up cache entries without taking a dependency on the internal
/// data-access contract.
/// </summary>
internal sealed class FederatedUserCacheReaderAdapter(IUserCacheStore store)
    : IFederatedUserCacheReader
{
    public Task<FederatedIdentity?> FindByExternalIdAsync(
        string externalUserId, Guid? tenantId, CancellationToken cancellationToken = default) =>
        store.FindByExternalIdAsync(externalUserId, tenantId, cancellationToken);
}
