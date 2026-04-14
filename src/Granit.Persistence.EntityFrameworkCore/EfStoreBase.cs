using System.Linq.Expressions;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.Specification;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.EntityFrameworkCore;

/// <summary>
/// Base class for EF Core reader/writer implementations. Eliminates the repeated
/// <see cref="IDbContextFactory{TContext}"/> boilerplate and provides CRUD helpers.
/// </summary>
/// <remarks>
/// <para>
/// This is an <b>implementation helper</b> — it does not appear in any module contract.
/// Domain-specific interfaces (<c>IXxxReader</c>/<c>IXxxWriter</c>) remain untouched.
/// Subclasses must be <c>internal sealed</c> (enforced by architecture tests).
/// </para>
/// <para>
/// Each method creates and disposes its own <typeparamref name="TContext"/> via the factory,
/// ensuring thread-safe concurrent access and fresh query filter evaluation per operation.
/// </para>
/// <para>
/// <b>Host context bypass:</b> When <paramref name="currentTenant"/> is provided and no
/// tenant is active (<see cref="ICurrentTenant.IsAvailable"/> is <c>false</c>), the
/// <see cref="GranitFilterNames.MultiTenant"/> named query filter is bypassed on all read
/// operations so the caller sees entities across all tenants. This is used for host-level
/// administration endpoints protected by <c>RequireHostContextEndpointFilter</c>.
/// All other filters (soft-delete, GDPR, active) remain active.
/// </para>
/// </remarks>
/// <typeparam name="TEntity">The entity type (must inherit <see cref="Entity"/>).</typeparam>
/// <typeparam name="TContext">The isolated <see cref="DbContext"/> type.</typeparam>
/// <param name="contextFactory">The factory for creating isolated <typeparamref name="TContext"/> instances.</param>
/// <param name="currentTenant">
/// Optional tenant context. When provided for <see cref="IMultiTenant"/> entities, enables
/// automatic host-context bypass of the multi-tenant query filter. Pass <c>null</c> (or omit)
/// to keep the default filtering behavior.
/// </param>
public abstract class EfStoreBase<TEntity, TContext>(
    IDbContextFactory<TContext> contextFactory,
    ICurrentTenant? currentTenant = null)
    where TEntity : Entity
    where TContext : DbContext
{
    // Pre-computed at construction (Scoped = once per request).
    // True only when TEntity implements IMultiTenant AND no tenant is active.
    private readonly bool _bypassTenantFilter =
        typeof(IMultiTenant).IsAssignableFrom(typeof(TEntity))
        && currentTenant is { IsAvailable: false };

    // ── Query helper ───────────────────────────────────────────────────

    /// <summary>
    /// Returns the base queryable for <typeparamref name="TEntity"/>.
    /// In host context (no active tenant), the <see cref="GranitFilterNames.MultiTenant"/>
    /// named query filter is bypassed so all entities are visible cross-tenant.
    /// All other filters (soft-delete, GDPR, active) remain active.
    /// </summary>
    /// <remarks>
    /// This method is safe to call in any context — when a tenant is active, it returns
    /// the standard <c>DbSet</c> with all filters applied.
    /// </remarks>
    protected IQueryable<TEntity> Query(TContext db) =>
        _bypassTenantFilter
            ? db.Set<TEntity>().IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : db.Set<TEntity>();

    // ── Read helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Finds an entity by its primary key. Uses <c>FirstOrDefaultAsync</c> instead of
    /// <c>FindAsync</c> to ensure all named query filters (tenant, soft-delete, GDPR
    /// processing restriction) are applied.
    /// </summary>
    /// <remarks>
    /// SECURITY (VULN-100): <c>DbSet.FindAsync()</c> bypasses all query filters.
    /// This method intentionally uses <c>FirstOrDefaultAsync(e =&gt; e.Id == id)</c>
    /// which preserves tenant isolation, soft-delete, and GDPR filters.
    /// </remarks>
    protected async Task<TEntity?> FindByIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        await using TContext db = await contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await Query(db)
            .FirstOrDefaultAsync(e => e.Id == id, ct).ConfigureAwait(false);
    }

    /// <summary>Finds the first entity matching the predicate, or <c>null</c>.</summary>
    protected async Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default)
    {
        await using TContext db = await contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await Query(db)
            .FirstOrDefaultAsync(predicate, ct).ConfigureAwait(false);
    }

    /// <summary>Returns entities matching the specification.</summary>
    protected async Task<IReadOnlyList<TEntity>> ListAsync(
        Specification<TEntity> spec,
        CancellationToken ct = default)
    {
        await using TContext db = await contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await SpecificationEvaluator
            .Apply(Query(db), spec)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Returns a paginated result for entities matching the specification.</summary>
    protected async Task<PagedResult<TEntity>> PagedAsync(
        Specification<TEntity> spec,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        await using TContext db = await contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await SpecificationEvaluator
            .Apply(Query(db), spec)
            .ToPagedResultAsync(page, pageSize, ct).ConfigureAwait(false);
    }

    /// <summary>Counts entities matching the optional predicate.</summary>
    protected async Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        await using TContext db = await contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return predicate is null
            ? await Query(db).CountAsync(ct).ConfigureAwait(false)
            : await Query(db).CountAsync(predicate, ct).ConfigureAwait(false);
    }

    /// <summary>Checks whether any entity matches the predicate.</summary>
    protected async Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default)
    {
        await using TContext db = await contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await Query(db).AnyAsync(predicate, ct).ConfigureAwait(false);
    }

    // ── Full DbContext access (complex queries, Include, joins) ────────

    /// <summary>Executes a read query with full DbContext access.</summary>
    /// <remarks>
    /// SECURITY (VULN-300): Query filters (tenant, soft-delete, GDPR) apply to LINQ
    /// queries but can be bypassed via <c>IgnoreQueryFilters()</c>. Any filter bypass
    /// must be justified and reviewed. Prefer typed helpers (<see cref="FindByIdAsync"/>,
    /// <see cref="ListAsync"/>) when possible.
    /// </remarks>
    protected async Task<TResult> ReadAsync<TResult>(
        Func<TContext, Task<TResult>> query,
        CancellationToken ct = default)
    {
        await using TContext db = await contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await query(db).ConfigureAwait(false);
    }

    // ── Write helpers ──────────────────────────────────────────────────

    /// <summary>Adds a new entity and saves.</summary>
    protected async Task AddAsync(
        TEntity entity,
        CancellationToken ct = default)
    {
        await using TContext db = await contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.Set<TEntity>().Add(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Marks an entity as modified and saves.</summary>
    protected async Task UpdateAsync(
        TEntity entity,
        CancellationToken ct = default)
    {
        await using TContext db = await contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.Set<TEntity>().Update(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Removes an entity and saves.</summary>
    protected async Task DeleteAsync(
        TEntity entity,
        CancellationToken ct = default)
    {
        await using TContext db = await contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.Set<TEntity>().Remove(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // ── Full DbContext access (state transitions, batch ops) ───────────

    /// <summary>Executes a write mutation with full DbContext access.</summary>
    /// <remarks>
    /// SECURITY (VULN-300): Provides unrestricted DbContext access. Query filters still
    /// apply to LINQ queries but can be bypassed. Any filter bypass must be justified.
    /// Prefer typed helpers (<see cref="AddAsync"/>, <see cref="UpdateAsync"/>,
    /// <see cref="DeleteAsync"/>) when possible.
    /// </remarks>
    protected async Task WriteAsync(
        Func<TContext, Task> mutation,
        CancellationToken ct = default)
    {
        await using TContext db = await contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await mutation(db).ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Executes a write mutation with full DbContext access and returns a result.</summary>
    /// <inheritdoc cref="WriteAsync(Func{TContext, Task}, CancellationToken)" path="/remarks"/>
    protected async Task<TResult> WriteAsync<TResult>(
        Func<TContext, Task<TResult>> mutation,
        CancellationToken ct = default)
    {
        await using TContext db = await contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        TResult result = await mutation(db).ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return result;
    }
}
