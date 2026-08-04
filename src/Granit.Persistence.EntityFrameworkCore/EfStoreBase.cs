using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Diagnostics;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.Specification;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
/// <b>Host context bypass (signaled only):</b> When <c>currentTenant</c> is provided and no
/// tenant is active (<see cref="ICurrentTenant.IsAvailable"/> is <c>false</c>), the
/// <see cref="GranitFilterNames.MultiTenant"/> named query filter is bypassed on read
/// operations <b>only</b> when the request carries an explicit host-access signal
/// (<see cref="IHostAccessContext.IsHostAccess"/>, set via <c>.AllowHostAccess()</c> behind
/// <c>RequireHostContextEndpointFilter</c>). An unsignaled absence of a tenant fails CLOSED —
/// the named filter stays active and only the host partition (<c>TenantId == null</c>) is
/// visible — so a tenant-context loss can never widen a query to every tenant. All other
/// filters (soft-delete, GDPR, active) remain active in both cases.
/// </para>
/// </remarks>
/// <typeparam name="TEntity">The entity type (must inherit <see cref="Entity"/>).</typeparam>
/// <typeparam name="TContext">The isolated <see cref="DbContext"/> type.</typeparam>
public abstract class EfStoreBase<TEntity, TContext>
    where TEntity : Entity
    where TContext : DbContext
{
    private static readonly bool s_isMultiTenantEntity =
        typeof(IMultiTenant).IsAssignableFrom(typeof(TEntity));

    private readonly IDbContextFactory<TContext> _contextFactory;
    private readonly ICurrentTenant? _currentTenant;
    private readonly IHostAccessContext? _hostAccess;
    private readonly PersistenceMetrics? _metrics;
    private readonly ILogger _logger;

    /// <param name="contextFactory">The factory for creating isolated <typeparamref name="TContext"/> instances.</param>
    /// <param name="currentTenant">
    /// Optional tenant context. When provided for <see cref="IMultiTenant"/> entities, enables
    /// automatic host-context bypass of the multi-tenant query filter. Pass <c>null</c> (or omit)
    /// to keep the default filtering behavior.
    /// </param>
    /// <param name="hostAccess">
    /// Optional host-access signal. Distinguishes a legitimate <c>.AllowHostAccess()</c> call
    /// (metric tag <c>host_endpoint</c>, log Information) from an unsignaled tenant-context
    /// loss (metric tag <c>implicit_unsignaled</c>, log Warning — alert-worthy). When omitted,
    /// every implicit bypass is reported as <c>implicit_unsignaled</c>.
    /// </param>
    /// <param name="metrics">
    /// Optional persistence metrics sink. When provided, every multi-tenant filter bypass is
    /// recorded as <c>granit.persistence.cross_tenant_query</c> tagged by entity and origin
    /// (<c>host_endpoint</c>, <c>implicit_unsignaled</c>, or <c>explicit</c>) — gives the SOC a
    /// signal to alert on anomalous cross-tenant read volume.
    /// </param>
    /// <param name="logger">
    /// Optional logger. When provided, unsignaled bypasses log a <see cref="LogLevel.Warning"/>
    /// (anomalous tenant-context loss) and signaled / explicit opt-ins log
    /// <see cref="LogLevel.Information"/> carrying the caller member / file / line —
    /// searchable trail for incident response that the metric alone cannot provide.
    /// Defaults to <see cref="NullLogger.Instance"/>.
    /// </param>
    protected EfStoreBase(
        IDbContextFactory<TContext> contextFactory,
        ICurrentTenant? currentTenant = null,
        IHostAccessContext? hostAccess = null,
        PersistenceMetrics? metrics = null,
        ILogger? logger = null)
    {
        _contextFactory = contextFactory;
        _currentTenant = currentTenant;
        _hostAccess = hostAccess;
        _metrics = metrics;
        _logger = logger ?? NullLogger.Instance;
    }

    // ── Query helper ───────────────────────────────────────────────────

    /// <summary>
    /// Returns the base queryable for <typeparamref name="TEntity"/>.
    /// In a <b>signaled</b> host context (no active tenant + <see cref="IHostAccessContext.IsHostAccess"/>),
    /// the <see cref="GranitFilterNames.MultiTenant"/> named query filter is bypassed so all entities
    /// are visible cross-tenant. An unsignaled tenant-context loss fails CLOSED (host partition only).
    /// All other filters (soft-delete, GDPR, active) remain active.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SECURITY: the tenant state is evaluated on every call instead of being cached at
    /// construction time, closing the edge case where a Scoped store was constructed
    /// before the tenant was activated and then served cross-tenant queries for the
    /// rest of the scope.
    /// </para>
    /// <para>
    /// A signaled host bypass is recorded as <c>granit.persistence.cross_tenant_query</c> with
    /// <c>origin=host_endpoint</c>; an unsignaled tenant-context loss fails CLOSED, is recorded
    /// with <c>origin=implicit_unsignaled</c>, and is logged at <see cref="LogLevel.Warning"/> for
    /// SOC alerting. New call-sites that need cross-tenant access should use the explicit
    /// <c>QueryAcrossTenants</c> (<c>origin=explicit</c>) rather than relying on host context.
    /// </para>
    /// </remarks>
    protected IQueryable<TEntity> Query(TContext db)
    {
        if (s_isMultiTenantEntity
            && _currentTenant is { IsAvailable: false }
            && CrossTenantFilterDecision.ShouldBypassMultiTenantFilter(
                typeof(TEntity).Name, _hostAccess, _metrics, _logger))
        {
            return db.Set<TEntity>().IgnoreQueryFilters([GranitFilterNames.MultiTenant]);
        }

        return db.Set<TEntity>();
    }

    /// <summary>
    /// Returns the base queryable with the <see cref="GranitFilterNames.MultiTenant"/>
    /// filter explicitly disabled. Use only for legitimate host-scope queries (admin
    /// dashboards, system health checks) where the caller is authorized to see data
    /// across tenants.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SECURITY: callers MUST gate access with <c>.AllowHostAccess()</c> or an equivalent
    /// authorization filter at the endpoint level. Misuse exposes cross-tenant data —
    /// every invocation is recorded as <c>granit.persistence.cross_tenant_query</c> with
    /// <c>origin=explicit</c> for SOC observability.
    /// </para>
    /// <para>
    /// Prefer this method over the host-context branch of <see cref="Query(TContext)"/>:
    /// it makes the intent visible in the call site and distinguishes the metric tag.
    /// The implicit branch is fail-closed — it never widens an unsignaled query to all
    /// tenants — so cross-tenant reads must come through here or a signaled host route.
    /// </para>
    /// </remarks>
    protected IQueryable<TEntity> QueryAcrossTenants(
        TContext db,
        [CallerMemberName] string callerMember = "",
        [CallerFilePath] string callerFile = "",
        [CallerLineNumber] int callerLine = 0)
    {
        string entity = typeof(TEntity).Name;
        string? tenantId = _currentTenant is { IsAvailable: true } tenant ? tenant.Id?.ToString() : null;
        _metrics?.RecordCrossTenantQuery(entity, "explicit", tenantId);
        EfStoreBaseLog.ExplicitCrossTenantQuery(_logger, entity, callerMember, callerFile, callerLine);
        return s_isMultiTenantEntity
            ? db.Set<TEntity>().IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : db.Set<TEntity>();
    }

    // ── Read helpers ────────────────────────────────────────────────────
    //
    // These helpers are read-only: each opens a throwaway context that is disposed
    // before the entity is returned, so the result is always detached. Writers
    // re-attach explicitly via db.Set<T>().Update(entity) in a separate context, so
    // change tracking here is pure overhead with no benefit — hence AsNoTracking().
    // The read -> mutate -> UpdateAsync flow is unaffected (Update() works on a
    // detached entity regardless of prior tracking state). Raw-context reads via
    // ReadAsync(...) keep their caller-controlled tracking.

    /// <summary>
    /// Finds an entity by its primary key. Uses <c>FirstOrDefaultAsync</c> instead of
    /// <c>FindAsync</c> to ensure all named query filters (tenant, soft-delete, GDPR
    /// processing restriction) are applied.
    /// </summary>
    /// <remarks>
    /// SECURITY: <c>DbSet.FindAsync()</c> bypasses all query filters.
    /// This method intentionally uses <c>FirstOrDefaultAsync(e =&gt; e.Id == id)</c>
    /// which preserves tenant isolation, soft-delete, and GDPR filters.
    /// </remarks>
    protected async Task<TEntity?> FindByIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        await using TContext db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await Query(db)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct).ConfigureAwait(false);
    }

    /// <summary>Finds the first entity matching the predicate, or <c>null</c>.</summary>
    protected async Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default)
    {
        await using TContext db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await Query(db)
            .AsNoTracking()
            .FirstOrDefaultAsync(predicate, ct).ConfigureAwait(false);
    }

    /// <summary>Returns entities matching the specification.</summary>
    protected async Task<IReadOnlyList<TEntity>> ListAsync(
        Specification<TEntity> spec,
        CancellationToken ct = default)
    {
        await using TContext db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await SpecificationEvaluator
            .Apply(Query(db), spec)
            .AsNoTracking()
            .ToListAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Returns a paginated result for entities matching the specification.</summary>
    protected async Task<PagedResult<TEntity>> PagedAsync(
        Specification<TEntity> spec,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        await using TContext db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await SpecificationEvaluator
            .Apply(Query(db), spec)
            .AsNoTracking()
            .ToPagedResultAsync(page, pageSize, ct).ConfigureAwait(false);
    }

    /// <summary>Counts entities matching the optional predicate.</summary>
    protected async Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        await using TContext db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return predicate is null
            ? await Query(db).CountAsync(ct).ConfigureAwait(false)
            : await Query(db).CountAsync(predicate, ct).ConfigureAwait(false);
    }

    /// <summary>Checks whether any entity matches the predicate.</summary>
    protected async Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default)
    {
        await using TContext db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await Query(db).AnyAsync(predicate, ct).ConfigureAwait(false);
    }

    // ── Full DbContext access (complex queries, Include, joins) ────────

    /// <summary>Executes a read query with full DbContext access.</summary>
    /// <remarks>
    /// SECURITY: Query filters (tenant, soft-delete, GDPR) apply to LINQ
    /// queries but can be bypassed via <c>IgnoreQueryFilters()</c>. Any filter bypass
    /// must be justified and reviewed. Prefer typed helpers (<see cref="FindByIdAsync"/>,
    /// <see cref="ListAsync"/>) when possible.
    /// </remarks>
    protected async Task<TResult> ReadAsync<TResult>(
        Func<TContext, Task<TResult>> query,
        CancellationToken ct = default)
    {
        await using TContext db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await query(db).ConfigureAwait(false);
    }

    // ── Write helpers ──────────────────────────────────────────────────

    /// <summary>Adds a new entity and saves.</summary>
    protected async Task AddAsync(
        TEntity entity,
        CancellationToken ct = default)
    {
        await using TContext db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.Set<TEntity>().Add(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Marks an entity as modified and saves.</summary>
    protected async Task UpdateAsync(
        TEntity entity,
        CancellationToken ct = default)
    {
        await using TContext db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.Set<TEntity>().Update(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Removes an entity and saves.</summary>
    protected async Task DeleteAsync(
        TEntity entity,
        CancellationToken ct = default)
    {
        await using TContext db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.Set<TEntity>().Remove(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // ── Full DbContext access (state transitions, batch ops) ───────────

    /// <summary>Executes a write mutation with full DbContext access.</summary>
    /// <remarks>
    /// SECURITY: Provides unrestricted DbContext access. Query filters still
    /// apply to LINQ queries but can be bypassed. Any filter bypass must be justified.
    /// Prefer typed helpers (<see cref="AddAsync"/>, <see cref="UpdateAsync"/>,
    /// <see cref="DeleteAsync"/>) when possible.
    /// </remarks>
    protected async Task WriteAsync(
        Func<TContext, Task> mutation,
        CancellationToken ct = default)
    {
        await using TContext db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await mutation(db).ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Executes a write mutation with full DbContext access and returns a result.</summary>
    /// <inheritdoc cref="WriteAsync(Func{TContext, Task}, CancellationToken)" path="/remarks"/>
    protected async Task<TResult> WriteAsync<TResult>(
        Func<TContext, Task<TResult>> mutation,
        CancellationToken ct = default)
    {
        await using TContext db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        TResult result = await mutation(db).ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return result;
    }
}
