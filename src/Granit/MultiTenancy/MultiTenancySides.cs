namespace Granit.MultiTenancy;

/// <summary>
/// Scope of an object (permission, BFF frontend, ...) relative to the host/tenant boundary.
/// </summary>
/// <remarks>
/// <para>
/// Declared in the foundation <c>Granit</c> assembly so it can be used wherever the
/// host/tenant concept is modeled (permission definitions, BFF frontends, future
/// extension points) without pulling a runtime dependency on the multi-tenancy module.
/// Consumers <c>using Granit.MultiTenancy;</c> get this enum from <c>Granit</c> and the
/// runtime services (<c>ICurrentTenant</c>, etc.) from <c>Granit.MultiTenancy</c>
/// transparently — they share the namespace, different assemblies.
/// </para>
/// <para>
/// Enforcement relies on <c>ICurrentTenant</c> (soft dependency on
/// <c>Granit.MultiTenancy</c>). When the multi-tenancy module is absent, the current
/// tenant is never available and only <see cref="Host"/> / <see cref="Both"/> permissions
/// are grantable — consumers without multi-tenancy simply don't declare <see cref="Tenant"/>
/// permissions, so the behavior remains coherent.
/// </para>
/// </remarks>
[Flags]
public enum MultiTenancySides
{
    /// <summary>Applicable only when no tenant context is active (host-level admin, cross-tenant operations).</summary>
    Host = 1 << 0,

    /// <summary>Applicable only when a tenant context is active (per-tenant operations).</summary>
    Tenant = 1 << 1,

    /// <summary>Applicable in both host and tenant contexts. Default for legacy declarations.</summary>
    Both = Host | Tenant,
}
