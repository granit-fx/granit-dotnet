namespace Granit.Identity.Federated.Internal;

/// <summary>
/// Adapter that exposes a minimal <see cref="IFederatedUserCacheEraser"/> view over the
/// internal <see cref="IUserCacheStore"/>. Lets downstream modules (notably the privacy
/// deletion handler) purge a cache entry without taking a dependency on the internal
/// data-access contract.
/// </summary>
internal sealed class FederatedUserCacheEraserAdapter(IUserCacheStore store)
    : IFederatedUserCacheEraser
{
    public Task EraseAsync(
        string externalUserId, Guid? tenantId, CancellationToken cancellationToken = default) =>
        store.DeleteByExternalIdAsync(externalUserId, tenantId, cancellationToken);
}
