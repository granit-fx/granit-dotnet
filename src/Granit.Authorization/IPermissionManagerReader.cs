namespace Granit.Authorization;

/// <summary>
/// Read-only service for querying role → permission grants.
/// Available only when <c>Granit.Authorization.EntityFrameworkCore</c> is registered.
/// </summary>
public interface IPermissionManagerReader
{
    /// <summary>Returns true if the role has been explicitly granted the permission.</summary>
    Task<bool> IsGrantedAsync(
        string permissionName,
        string roleName,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns all permission names explicitly granted to the role in the given tenant.</summary>
    Task<IReadOnlyList<string>> GetGrantedPermissionsAsync(
        string roleName,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns all role names that have been explicitly granted the permission in the given tenant.</summary>
    Task<IReadOnlyList<string>> GetGrantedRolesAsync(
        string permissionName,
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}
