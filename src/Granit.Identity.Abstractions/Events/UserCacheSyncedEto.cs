using Granit.Events;

namespace Granit.Identity.Events;

/// <summary>
/// Published when a user cache entry is synchronized from the external identity provider.
/// Enables profile change propagation to dependent modules.
/// </summary>
/// <param name="ExternalUserId">The identity provider user identifier.</param>
/// <param name="TenantId">Tenant scope. <c>null</c> for host-level entries.</param>
/// <param name="SyncedAt">Timestamp of the synchronization.</param>
public sealed record UserCacheSyncedEto(
    string ExternalUserId,
    Guid? TenantId,
    DateTimeOffset SyncedAt) : IIntegrationEvent;
