namespace Granit.Authorization.Events;

/// <summary>
/// Raised when a permission grant is created or revoked for a role.
/// </summary>
/// <remarks>
/// Consumed by <see cref="Cache.PermissionCacheInvalidationHandler"/> to remove the stale
/// entry from <see cref="Caching.ICacheService{PermissionGrantCacheItem}"/>.
/// Publish this event after any <see cref="Abstractions.IPermissionManagerWriter"/> mutation.
/// </remarks>
/// <param name="PermissionName">The permission that was granted or revoked.</param>
/// <param name="RoleName">The role whose grant changed.</param>
/// <param name="TenantId">The tenant scope, or <c>null</c> for a global change.</param>
/// <param name="IsGranted">Whether the permission was granted (<c>true</c>) or revoked (<c>false</c>).</param>
public sealed record PermissionGrantChangedEvent(
    string PermissionName,
    string RoleName,
    Guid? TenantId,
    bool IsGranted);
