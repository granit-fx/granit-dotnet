namespace Granit.Persistence.MultiTenancy;

/// <summary>
/// Physical storage layout choice for entities whose semantic applicability is
/// <c>MultiTenancySides.Both</c> — i.e. dual-scope modules where the same entity shape
/// holds both host-owned and tenant-owned rows.
/// </summary>
/// <remarks>
/// <para>
/// Complements <c>Granit.MultiTenancy.MultiTenancySides</c>, which describes the
/// applicability semantics of a capability or entity (Host-only, Tenant-only, or Both).
/// When applicability is <c>Both</c>, this enum picks the physical layout — a single
/// shared host table with a row-level filter, or two physically separated tables.
/// </para>
/// <para>
/// <see cref="Shared"/> is the framework default and reproduces the pre-ADR-063 behaviour
/// across all dual-scope modules. <see cref="Segregated"/> opts the module into a
/// two-context shape where host rows live in the host schema and tenant rows live in the
/// tenant schema (or per-tenant database) for defense-in-depth against row-level filter
/// regressions and to make <c>DROP SCHEMA &lt;tenant&gt; CASCADE</c> lessivage natively
/// work for RGPD Art. 17.
/// </para>
/// <para>
/// Per ADR-063, this enum is the single framework-level vocabulary for the choice; each
/// dual-scope module exposes it through its <c>*EntityFrameworkCoreOptions.StorageMode</c>
/// property, defaulting to <see cref="Shared"/>. <see cref="Segregated"/> support is rolled
/// out module-by-module starting with <c>Granit.Webhooks</c> (Epic #2377).
/// </para>
/// </remarks>
public enum DualScopeStorageMode
{
    /// <summary>
    /// Single physical table in the host schema; <c>TenantId</c> is nullable. Host rows have
    /// <c>TenantId == null</c>, tenant rows carry the owning tenant's identifier. Scoping is
    /// enforced per-request by an EF Named Query Filter on <c>TenantId</c>.
    /// </summary>
    /// <remarks>
    /// Lowest infrastructure cost and lowest provisioning ceremony — host and tenant data
    /// live in the same table and migrations apply uniformly. This is the framework default.
    /// </remarks>
    Shared = 0,

    /// <summary>
    /// Two physical tables: a host-pinned table for host-scope rows, plus a per-tenant table
    /// (in the tenant schema or per-tenant database) for tenant-scope rows. No row-level
    /// filter — separation is physical. The module's store dispatches reads and writes to
    /// the appropriate context based on the active scope.
    /// </summary>
    /// <remarks>
    /// Provides defense-in-depth against row-level filter regressions and enables native
    /// per-tenant data lessivage via schema or database drop. Requires either
    /// <c>SchemaPerTenant</c> or <c>DatabasePerTenant</c> tenant isolation; combining
    /// <see cref="Segregated"/> with <c>SharedDatabase</c> is rejected at startup because it
    /// provides no physical isolation despite advertising it.
    /// </remarks>
    Segregated = 1,
}
