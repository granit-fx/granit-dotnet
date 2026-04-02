namespace Granit.Authorization.Abstractions;

/// <summary>
/// Data access layer for permission grants.
/// Called by <see cref="IPermissionChecker"/> (read) and <c>PermissionManager</c> (read/write).
/// Default implementation is <c>NullPermissionGrantStore</c> (always false, writes are no-ops).
/// Override with <c>Granit.Authorization.EntityFrameworkCore</c> for persistence.
/// </summary>
public interface IPermissionGrantStore
{
    /// <summary>
    /// Returns true if the specified role has been explicitly granted the permission in the given tenant.
    /// </summary>
    Task<bool> IsGrantedAsync(
        string roleName,
        string permissionName,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all permission names explicitly granted to the role in the given tenant.
    /// </summary>
    Task<IReadOnlyList<string>> GetGrantedPermissionsAsync(
        string roleName,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all role names that have been explicitly granted the permission in the given tenant.
    /// </summary>
    Task<IReadOnlyList<string>> GetGrantedRolesAsync(
        string permissionName,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a permission grant for the specified role in the given tenant.
    /// Returns true if the grant was created, false if it already existed (idempotent).
    /// </summary>
    Task<bool> GrantAsync(
        string permissionName,
        string roleName,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a permission grant for the specified role in the given tenant.
    /// Returns true if the grant was removed, false if it did not exist (idempotent).
    /// </summary>
    Task<bool> RevokeAsync(
        string permissionName,
        string roleName,
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}
