using Granit.Domain;

namespace Granit.Authorization.Domain;

/// <summary>
/// Represents an explicit grant of a permission to a role within a tenant.
/// RBAC strict: no UserId column — permissions are assigned to roles, never to individual users.
/// Audited via <see cref="AuditedEntity"/> interceptor (CreatedAt, CreatedBy, ModifiedAt, ModifiedBy).
/// </summary>
public sealed class PermissionGrant : AuditedEntity, IMultiTenant
{
    /// <summary>Permission name, e.g. "Invoices.Delete". Max 256 characters.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Role name from the identity provider (Keycloak realm role, ASP.NET Core Identity role, etc.).
    /// Max 256 characters.
    /// </summary>
    public string RoleName { get; init; } = string.Empty;

    /// <summary>Tenant scope. Null means the grant applies at the host (cross-tenant) level.</summary>
    /// <remarks>Keeps <c>set</c> to satisfy <see cref="IMultiTenant"/> interceptor injection.</remarks>
    public Guid? TenantId { get; set; }
}
