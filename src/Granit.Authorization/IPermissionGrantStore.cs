namespace Granit.Authorization;

/// <summary>
/// Data access layer for permission grants. Operates on the ABP-style
/// <c>(providerName, providerKey, permissionName, tenantId)</c> tuple.
/// Called by <see cref="IPermissionChecker"/> (read) and <c>PermissionManager</c> (read/write).
/// Default implementation is <c>NullPermissionGrantStore</c> (always false, writes are no-ops).
/// Override with <c>Granit.Authorization.EntityFrameworkCore</c> for persistence.
/// </summary>
public interface IPermissionGrantStore
{
    /// <summary>
    /// Returns <see langword="true"/> if the specified grantee has been explicitly granted
    /// <paramref name="permissionName"/> in the given tenant.
    /// </summary>
    Task<bool> IsGrantedAsync(
        string providerName,
        string providerKey,
        string permissionName,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all permission names explicitly granted to the grantee in the given tenant.
    /// </summary>
    Task<IReadOnlyList<string>> GetGrantedPermissionsAsync(
        string providerName,
        string providerKey,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the subset of <paramref name="permissionNames"/> that are explicitly granted
    /// to the grantee in the given tenant. Filters with a single <c>WHERE IN</c> clause.
    /// </summary>
    Task<IReadOnlyList<string>> GetGrantedAsync(
        string providerName,
        string providerKey,
        IReadOnlyList<string> permissionNames,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all grantee keys of the specified <paramref name="providerName"/> that have
    /// been explicitly granted <paramref name="permissionName"/> in the given tenant.
    /// </summary>
    Task<IReadOnlyList<string>> GetGranteesAsync(
        string providerName,
        string permissionName,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a permission grant for the specified grantee in the given tenant.
    /// Returns <see langword="true"/> if the grant was created, <see langword="false"/> if it already existed (idempotent).
    /// </summary>
    Task<bool> GrantAsync(
        string providerName,
        string providerKey,
        string permissionName,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a permission grant for the specified grantee in the given tenant.
    /// Returns <see langword="true"/> if the grant was removed, <see langword="false"/> if it did not exist (idempotent).
    /// </summary>
    Task<bool> RevokeAsync(
        string providerName,
        string providerKey,
        string permissionName,
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}
