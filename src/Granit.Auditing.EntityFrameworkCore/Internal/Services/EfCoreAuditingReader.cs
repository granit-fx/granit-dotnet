using System.Linq.Expressions;
using System.Reflection;
using Granit.Auditing.Domain;
using Granit.Auditing.Extensions;
using Granit.Auditing.Options;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Auditing.EntityFrameworkCore.Internal.Services;

/// <summary>
/// EF Core implementation of <see cref="IAuditingReader"/> with FusionCache
/// for immutable audit entries and short-lived entity query results.
/// </summary>
internal sealed class EfCoreAuditingReader(
    IDbContextFactory<AuditingDbContext> dbContextFactory,
    IFusionCache cache,
    ICurrentTenant currentTenant,
    IEnumerable<IAuditEntityTypeAliasProvider> aliasProviders,
    IOptions<AuditingOptions> options) : IAuditingReader
{
    private readonly AuditingOptions _options = options.Value;
    private string TenantCachePrefix => currentTenant is { IsAvailable: true, Id: { } id } ? id.ToString() : "global";

    /// <inheritdoc/>
    public async Task<AuditEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"audit:{TenantCachePrefix}:entry:{id}";

        MaybeValue<AuditEntry?> maybe = await cache.TryGetAsync<AuditEntry?>(cacheKey, token: cancellationToken).ConfigureAwait(false);
        if (maybe.HasValue)
        {
            return maybe.Value;
        }

        await using AuditingDbContext dbContext = await dbContextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        AuditEntry? entry = await dbContext.AuditEntries
            .Include(e => e.EntityChanges)
                .ThenInclude(ec => ec.PropertyChanges)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entry is not null)
        {
            await cache.SetAsync(cacheKey, entry, new FusionCacheEntryOptions { Duration = _options.CacheEntryTtl }, token: cancellationToken).ConfigureAwait(false);
        }

        return entry;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<AuditEntry>> GetByEntityAsync(
        string entityType,
        string entityId,
        int page = 1,
        int pageSize = QueryEngineDefaults.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        string cacheKey = $"audit:{TenantCachePrefix}:entity:{Uri.EscapeDataString(entityType)}:{Uri.EscapeDataString(entityId)}:p{page}:s{pageSize}";

        MaybeValue<PagedResult<AuditEntry>?> maybe = await cache.TryGetAsync<PagedResult<AuditEntry>?>(cacheKey, token: cancellationToken).ConfigureAwait(false);
        if (maybe.HasValue)
        {
            return maybe.Value!;
        }

        await using AuditingDbContext dbContext = await dbContextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Resolve every CLR type the audit log may have stamped for this
        // canonical name (ADR-051 split persistence). Materialise to string[]
        // because Npgsql translates Contains on arrays/lists to a SQL IN-list
        // but does not recognise IReadOnlySet<string> — the query would fall
        // back to client evaluation (silently empty on InMemory in tests, hard
        // failure on Postgres at runtime).
        string[] matchTypes = [.. aliasProviders.Resolve(entityType)];

        IQueryable<AuditEntry> queryable = dbContext.AuditEntries
            .Include(e => e.EntityChanges)
                .ThenInclude(ec => ec.PropertyChanges)
            .AsSplitQuery()
            .Where(e => e.EntityChanges.Any(ec =>
                matchTypes.Contains(ec.EntityType) && ec.EntityId == entityId))
            .AsNoTracking();

        PagedResult<AuditEntry> result = await queryable
            .OrderByDescending(e => e.Timestamp)
            .ToPagedResultAsync(page, pageSize, cancellationToken)
            .ConfigureAwait(false);

        await cache.SetAsync(cacheKey, result, new FusionCacheEntryOptions { Duration = _options.CacheEntityQueryTtl }, token: cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AuditEntry>> GetByEntitiesAsync(
        IReadOnlyCollection<AuditEntityRef> targets,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        if (targets.Count == 0)
        {
            return [];
        }

        // Group by EntityType so each scope becomes ONE predicate branch
        // (ec.EntityType == type AND ids.Contains(ec.EntityId)) and the
        // branches OR together. Strict pair semantics — no cross-type bleed,
        // no reliance on Guid cross-table uniqueness as an implicit invariant.
        Expression<Func<AuditEntityChange, bool>>? predicate = null;
        ParameterExpression ecParam = Expression.Parameter(typeof(AuditEntityChange), "ec");

        foreach (IGrouping<string, AuditEntityRef> group in targets.GroupBy(t => t.EntityType, StringComparer.Ordinal))
        {
            // Materialise to string[] so Npgsql translates Contains to a SQL
            // IN-list (the same constraint observed at GetByEntityAsync above).
            string[] ids = [.. group.Select(t => t.EntityId).Distinct(StringComparer.Ordinal)];
            if (ids.Length == 0)
            {
                continue;
            }

            Expression<Func<AuditEntityChange, bool>> branch =
                ec => ec.EntityType == group.Key && ids.Contains(ec.EntityId);

            Expression rebound = ParameterRebinder.Replace(branch.Body, branch.Parameters[0], ecParam);
            predicate = predicate is null
                ? Expression.Lambda<Func<AuditEntityChange, bool>>(rebound, ecParam)
                : Expression.Lambda<Func<AuditEntityChange, bool>>(
                    Expression.OrElse(predicate.Body, rebound), ecParam);
        }

        if (predicate is null)
        {
            return [];
        }

        // Hoist the predicate into the outer .Any() — that subquery is what the
        // covering index (EntityType, EntityId, AuditEntryId) was added for.
        ParameterExpression entryParam = Expression.Parameter(typeof(AuditEntry), "e");
        MemberExpression changes = Expression.Property(entryParam, nameof(AuditEntry.EntityChanges));
        MethodCallExpression any = Expression.Call(AnyMethodInfo, changes, predicate);
        var outer = Expression.Lambda<Func<AuditEntry, bool>>(any, entryParam);

        await using AuditingDbContext dbContext = await dbContextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // No FusionCache on the batch path — per-request target sets cause
        // cache-key explosion and the merger only fetches top-K, so any hit
        // rate would be coincidental at best.
        return await dbContext.AuditEntries
            .Include(e => e.EntityChanges)
                .ThenInclude(ec => ec.PropertyChanges)
            .AsSplitQuery()
            .Where(outer)
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<List<AuditEntry>> GetByUserAsync(
        string userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        await using AuditingDbContext dbContext = await dbContextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await dbContext.AuditEntries
            .Include(e => e.EntityChanges)
                .ThenInclude(ec => ec.PropertyChanges)
            .AsSplitQuery()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<AuditEntry>> GetByCorrelationIdAsync(
        string correlationId,
        int page = 1,
        int pageSize = QueryEngineDefaults.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        await using AuditingDbContext dbContext = await dbContextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await dbContext.AuditEntries
            .Include(e => e.EntityChanges)
                .ThenInclude(ec => ec.PropertyChanges)
            .AsSplitQuery()
            .Where(e => e.CorrelationId == correlationId)
            .OrderByDescending(e => e.Timestamp)
            .AsNoTracking()
            .ToPagedResultAsync(page, pageSize, cancellationToken)
            .ConfigureAwait(false);
    }

    private static readonly MethodInfo AnyMethodInfo = typeof(Enumerable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(Enumerable.Any)
            && m.GetParameters().Length == 2)
        .MakeGenericMethod(typeof(AuditEntityChange));

    /// <summary>
    /// Rewrites every reference to <c>from</c> in an expression tree as
    /// <c>to</c>. Lets us re-use the body of an <c>Expression{Func{T,bool}}</c>
    /// under a fresh parameter when combining per-scope predicates via
    /// <c>Expression.OrElse</c>.
    /// </summary>
    private sealed class ParameterRebinder(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        public static Expression Replace(Expression body, ParameterExpression from, ParameterExpression to) =>
            new ParameterRebinder(from, to).Visit(body);

        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }
}
