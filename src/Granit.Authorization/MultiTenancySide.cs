namespace Granit.Authorization;

/// <summary>
/// Scope of a permission relative to the host/tenant boundary.
/// </summary>
/// <remarks>
/// Evaluated by <see cref="IPermissionChecker"/> before grant lookup, using <c>ICurrentTenant</c>
/// (soft dependency on <c>Granit.MultiTenancy</c>). When the multi-tenancy module is absent, the
/// current tenant is never available and only <see cref="Host"/> / <see cref="Both"/> permissions
/// are grantable — consumers without multi-tenancy simply don't declare <see cref="Tenant"/>
/// permissions, so the behavior remains coherent.
/// </remarks>
[Flags]
public enum MultiTenancySide
{
    /// <summary>Permission is grantable only when no tenant context is active (host-level admin, cross-tenant operations).</summary>
    Host = 1 << 0,

    /// <summary>Permission is grantable only when a tenant context is active (per-tenant operations).</summary>
    Tenant = 1 << 1,

    /// <summary>Permission is grantable in both host and tenant contexts. Default for legacy permissions.</summary>
    Both = Host | Tenant,
}
