using Granit.Core.Events;

namespace Granit.Identity.Events;

/// <summary>
/// Published when a user cache entry is permanently deleted (RGPD Art. 17 — right to erasure).
/// Provides an audit trail for ISO 27001 compliance and enables downstream modules to react
/// to user data removal (e.g., clearing related caches, triggering cascade erasure).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Entities.UserCacheEntry"/> is an anemic entity (not an aggregate root), so this
/// event cannot be raised from the entity itself. It should be dispatched by the store or service
/// that performs the deletion (e.g., <c>IUserCacheStore.DeleteByExternalIdAsync</c> implementation).
/// </para>
/// </remarks>
/// <param name="ExternalUserId">The user identifier in the external identity provider.</param>
/// <param name="TenantId">The tenant scope of the erased entry, or <c>null</c> for host-level entries.</param>
/// <param name="ErasedAt">The timestamp when the cache entry was erased.</param>
public sealed record UserCacheEntryErasedEto(
    string ExternalUserId,
    Guid? TenantId,
    DateTimeOffset ErasedAt) : IIntegrationEvent;
