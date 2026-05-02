namespace Granit.Documents;

/// <summary>
/// Lazy, idempotent bootstrap of per-tenant infrastructure rows that
/// <c>Granit.Documents</c> requires before any user-facing operation.
/// </summary>
/// <remarks>
/// <para>
/// In phase 1 the only bootstrapped row is the invisible tenant root <c>Folder</c>
/// (see ADR-052 §<i>Tenant root folder bootstrap</i>). Every <c>Granit.Documents</c>
/// write endpoint calls <see cref="EnsureTenantRootAsync"/> before it touches the
/// folder tree so the root is guaranteed to exist for the current tenant — without
/// any coupling to <c>Granit.MultiTenancy</c>'s <c>TenantCreatedEto</c>.
/// </para>
/// <para>
/// The implementation is scoped (one instance per request) and memoises the
/// resolved root identifier per <c>TenantId</c> for the lifetime of the scope, so
/// repeated calls within the same request never re-issue a SELECT.
/// </para>
/// <para>
/// Concurrency: when two requests bootstrap the same tenant simultaneously, the
/// SELECT-then-INSERT pattern relies on the partial unique index
/// <c>ux_documents_folders_one_root_per_tenant</c> to keep exactly one root row per
/// tenant; the second INSERT either fails with a unique-constraint violation or is
/// no-op'd (Postgres <c>ON CONFLICT DO NOTHING</c>). The implementation handles both
/// cases by re-issuing the SELECT to obtain the winning row's identifier.
/// </para>
/// </remarks>
public interface IDocumentBootstrapService
{
    /// <summary>
    /// Returns the identifier of the tenant root folder for <paramref name="tenantId"/>,
    /// creating it on first access. Idempotent and concurrency-safe.
    /// </summary>
    /// <param name="tenantId">The tenant whose root folder must be ensured. <c>null</c> targets the host scope.</param>
    /// <param name="ownerUserId">User identifier recorded as the owner of the root folder when it has to be created.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>The identifier of the (now-existing) tenant root folder.</returns>
    Task<Guid> EnsureTenantRootAsync(
        Guid? tenantId,
        Guid ownerUserId,
        CancellationToken cancellationToken = default);
}
