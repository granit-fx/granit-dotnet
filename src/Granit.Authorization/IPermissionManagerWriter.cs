namespace Granit.Authorization.Abstractions;

/// <summary>
/// Administrative service for mutating role → permission grants.
/// Each call to <see cref="SetAsync"/> emits an ISO 27001 audit log entry and invalidates the cache.
/// Available only when <c>Granit.Authorization.EntityFrameworkCore</c> is registered.
/// </summary>
public interface IPermissionManagerWriter
{
    /// <summary>
    /// Grants or revokes a permission for a role within a tenant.
    /// No-op if the current state already matches <paramref name="isGranted"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="permissionName"/> has not been declared.
    /// </exception>
    Task SetAsync(
        string permissionName,
        string roleName,
        Guid? tenantId,
        bool isGranted,
        CancellationToken cancellationToken = default);
}
