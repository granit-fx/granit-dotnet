using Granit.Events;

namespace Granit.Authorization.Events;

/// <summary>
/// Raised when a permission grant is created or revoked for a grantee (role, user, or
/// OIDC client).
/// </summary>
/// <remarks>
/// Domain event collected from <see cref="Domain.PermissionGrant"/> via
/// <c>AddDomainEvent</c> (create/revoke paths) and dispatched by the
/// <c>DomainEventDispatcherInterceptor</c> after <c>SaveChanges</c> commits. Consumed by
/// <see cref="Cache.PermissionCacheInvalidationHandler"/> to invalidate the stale entry.
/// </remarks>
/// <param name="PermissionName">The permission that was granted or revoked.</param>
/// <param name="ProviderName">Provider identifier for the grantee (<c>"R"</c>, <c>"U"</c>, <c>"C"</c>).</param>
/// <param name="ProviderKey">Provider-specific key (role name, user id, client id).</param>
/// <param name="TenantId">The tenant scope, or <c>null</c> for a host-level change.</param>
/// <param name="IsGranted">Whether the permission was granted (<c>true</c>) or revoked (<c>false</c>).</param>
public sealed record PermissionGrantChangedEvent(
    string PermissionName,
    string ProviderName,
    string ProviderKey,
    Guid? TenantId,
    bool IsGranted) : IDomainEvent;
