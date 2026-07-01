using Granit.MultiTenancy;

namespace Granit.Persistence.EntityFrameworkCore;

/// <summary>
/// Applies the framework's "absent tenant context ⇒ fail-closed unless signaled" decision to a
/// raw <see cref="IQueryable{T}"/> exposed by an <c>IQueryableSource&lt;T&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is the QueryEngine counterpart of the CRUD-path guard in
/// <see cref="EfStoreBase{TEntity, TContext}"/>: both share the same decision
/// (<see cref="Diagnostics.CrossTenantFilterDecision"/>), so a tenant-context loss on the
/// query path can never widen a read to every tenant. Register via
/// <c>services.AddGranitPersistence()</c> (TryAddScoped) — every
/// <c>*.EntityFrameworkCore</c> package already references this assembly per the isolated
/// DbContext convention.
/// </para>
/// <para>
/// Scoped: it reads the request-local <see cref="ICurrentTenant"/> and
/// <see cref="IHostAccessContext"/> live per call, never caching the tenant state at
/// construction time (which is the exact fail-open shape this fix removes).
/// </para>
/// </remarks>
public interface ITenantQueryScope
{
    /// <summary>
    /// Restricts <paramref name="query"/> to the caller's tenant, applying the multi-tenant
    /// named filter bypass only for a signaled host-access request. For a non-<c>IMultiTenant</c>
    /// entity the query is returned unchanged.
    /// </summary>
    /// <typeparam name="TEntity">The queried entity type.</typeparam>
    /// <param name="query">The base queryable (already carrying the model's named filters).</param>
    /// <param name="entityName">
    /// The CLR name of <typeparamref name="TEntity"/> for metric/log tagging — pass
    /// <c>typeof(TEntity).Name</c> (<c>nameof</c> on a type parameter yields <c>"TEntity"</c>).
    /// </param>
    /// <returns>
    /// The queryable with the multi-tenant filter bypassed (signaled host access) or left intact
    /// (fail-closed default).
    /// </returns>
    IQueryable<TEntity> Restrict<TEntity>(IQueryable<TEntity> query, string entityName)
        where TEntity : class;
}
