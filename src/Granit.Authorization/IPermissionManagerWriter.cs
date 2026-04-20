namespace Granit.Authorization;

/// <summary>
/// Administrative service for mutating grants in the ABP-style
/// <c>(providerName, providerKey)</c> model.
/// Each call to <see cref="SetAsync"/> runs the <see cref="IPermissionGrantValidator"/> chain
/// (for additions), emits an ISO 27001 audit log entry, and invalidates the cache.
/// Available only when <c>Granit.Authorization.EntityFrameworkCore</c> is registered.
/// </summary>
public interface IPermissionManagerWriter
{
    /// <summary>
    /// Grants or revokes a permission for a grantee within a tenant.
    /// No-op if the current state already matches <paramref name="isGranted"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="permissionName"/> has not been declared, or if any
    /// <see cref="IPermissionGrantValidator"/> rejects the grant.
    /// </exception>
    Task SetAsync(
        string permissionName,
        string providerName,
        string providerKey,
        Guid? tenantId,
        bool isGranted,
        CancellationToken cancellationToken = default);
}
