namespace Granit.Authorization;

/// <summary>
/// Read-only service for querying grant state via the ABP-style
/// <c>(providerName, providerKey)</c> tuple.
/// Available only when <c>Granit.Authorization.EntityFrameworkCore</c> is registered.
/// </summary>
public interface IPermissionManagerReader
{
    /// <summary>Returns true if the grantee has been explicitly granted the permission.</summary>
    Task<bool> IsGrantedAsync(
        string permissionName,
        string providerName,
        string providerKey,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns all permission names explicitly granted to the grantee in the given tenant.</summary>
    Task<IReadOnlyList<string>> GetGrantedPermissionsAsync(
        string providerName,
        string providerKey,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all grantee keys of the specified <paramref name="providerName"/> that have been
    /// explicitly granted <paramref name="permissionName"/> in the given tenant.
    /// </summary>
    Task<IReadOnlyList<string>> GetGranteesAsync(
        string providerName,
        string permissionName,
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}
