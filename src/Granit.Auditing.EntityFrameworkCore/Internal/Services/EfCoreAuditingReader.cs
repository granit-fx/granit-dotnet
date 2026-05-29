using System.Linq.Expressions;
using System.Reflection;
using Granit.Auditing.Domain;
using Granit.Auditing.Extensions;
using Granit.Auditing.Options;
using Granit.MultiTenancy;
using Granit.Persistence.MultiTenancy;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Auditing.EntityFrameworkCore.Internal.Services;

/// <summary>
/// EF Core implementation of <see cref="IAuditingReader"/> with FusionCache for immutable
/// audit entries and short-lived entity query results. Dispatches through
/// <see cref="AuditingContextResolver"/>.
/// </summary>
/// <remarks>
/// <para>
/// Tenant-scoped reads hit the right physical context and use a tenant-prefixed cache key.
/// Host-admin reads (no ambient tenant) under <c>Shared</c> mode bypass the MultiTenant
/// filter on the single host context. Under <c>Segregated</c> mode they materialise across
/// the host context plus every tenant returned by <see cref="ITenantsAccessor"/> via
/// <see cref="ICurrentTenant.Change"/> — cross-tenant SOC2 review remains possible.
/// </para>
/// </remarks>
internal sealed class EfCoreAuditingReader(
    AuditingContextResolver resolver,
    IFusionCache cache,
    ICurrentTenant currentTenant,
    ITenantsAccessor tenantsAccessor,
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

        AuditEntry? entry = await ResolveAcrossScopesAsync(
            async db => await db.AuditEntries
                .Include(e => e.EntityChanges)
                    .ThenInclude(ec => ec.PropertyChanges)
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false),
            firstNonNull: true,
            cancellationToken).ConfigureAwait(false);

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

        string[] matchTypes = [.. aliasProviders.Resolve(entityType)];

        // Tenant-scoped or Shared mode → single context; Segregated + host-admin →
        // materialise across all contexts, then in-memory paginate.
        IReadOnlyList<AuditEntry> allMatching = await CollectAcrossScopesAsync(
            async db => await db.AuditEntries
                .Include(e => e.EntityChanges)
                    .ThenInclude(ec => ec.PropertyChanges)
                .Where(e => e.EntityChanges.Any(ec =>
                    matchTypes.Contains(ec.EntityType) && ec.EntityId == entityId))
                .AsNoTracking()
                .ToListAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        List<AuditEntry> ordered = [.. allMatching.OrderByDescending(e => e.Timestamp)];
        int totalCount = ordered.Count;
        int skip = (page - 1) * pageSize;
        IReadOnlyList<AuditEntry> items = ordered.Skip(skip).Take(pageSize).ToList();

        PagedResult<AuditEntry> result = new(items, totalCount, HasMore: (skip + pageSize) < totalCount);

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
        // (ec.EntityType == type AND ids.Contains(ec.EntityId)) and the branches OR together.
        Expression<Func<AuditEntityChange, bool>>? predicate = null;
        ParameterExpression ecParam = Expression.Parameter(typeof(AuditEntityChange), "ec");

        foreach (IGrouping<string, AuditEntityRef> group in targets.GroupBy(t => t.EntityType, StringComparer.Ordinal))
        {
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

        ParameterExpression entryParam = Expression.Parameter(typeof(AuditEntry), "e");
        MemberExpression changes = Expression.Property(entryParam, nameof(AuditEntry.EntityChanges));
        MethodCallExpression any = Expression.Call(AnyMethodInfo, changes, predicate);
        var outer = Expression.Lambda<Func<AuditEntry, bool>>(any, entryParam);

        IReadOnlyList<AuditEntry> all = await CollectAcrossScopesAsync(
            async db => await db.AuditEntries
                .Include(e => e.EntityChanges)
                    .ThenInclude(ec => ec.PropertyChanges)
                .Where(outer)
                .AsNoTracking()
                .ToListAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return [.. all.OrderByDescending(e => e.Timestamp).Take(limit)];
    }

    /// <inheritdoc/>
    public async Task<List<AuditEntry>> GetByUserAsync(
        string userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        IReadOnlyList<AuditEntry> all = await CollectAcrossScopesAsync(
            async db => await db.AuditEntries
                .Include(e => e.EntityChanges)
                    .ThenInclude(ec => ec.PropertyChanges)
                .Where(e => e.UserId == userId)
                .AsNoTracking()
                .ToListAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return [.. all.OrderByDescending(e => e.Timestamp).Take(limit)];
    }

    /// <inheritdoc/>
    public async Task<List<AuditEntry>> GetByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        IReadOnlyList<AuditEntry> all = await CollectAcrossScopesAsync(
            async db => await db.AuditEntries
                .Include(e => e.EntityChanges)
                    .ThenInclude(ec => ec.PropertyChanges)
                .Where(e => e.CorrelationId == correlationId)
                .AsNoTracking()
                .ToListAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return [.. all.OrderByDescending(e => e.Timestamp)];
    }

    /// <summary>
    /// Runs <paramref name="query"/> against the appropriate context(s) and returns the
    /// first non-null result (used for id-based lookups). Under Segregated + host-admin
    /// scope, iterates host + every tenant; otherwise opens a single context for the
    /// caller's scope.
    /// </summary>
    private async Task<TResult?> ResolveAcrossScopesAsync<TResult>(
        Func<IAuditingDbContext, Task<TResult?>> query,
        bool firstNonNull,
        CancellationToken cancellationToken)
        where TResult : class
    {
        if (resolver.StorageMode == DualScopeStorageMode.Segregated && !currentTenant.IsAvailable)
        {
            IReadOnlyList<IAuditingDbContext> contexts = await resolver
                .OpenAllAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                foreach (IAuditingDbContext ctx in contexts)
                {
                    TResult? hit = await query(ctx).ConfigureAwait(false);
                    if (hit is not null && firstNonNull)
                    {
                        return hit;
                    }
                }
                return null;
            }
            finally
            {
                await DisposeAllAsync(contexts).ConfigureAwait(false);
            }
        }

        await using IAuditingDbContext singleDb = await resolver
            .OpenForScopeAsync(currentTenant.IsAvailable ? currentTenant.Id : null, cancellationToken)
            .ConfigureAwait(false);
        return await query(singleDb).ConfigureAwait(false);
    }

    /// <summary>
    /// Materialises a list-returning query across all candidate contexts. Under
    /// <c>Segregated</c> + host-admin scope, iterates host + every tenant via
    /// <see cref="ITenantsAccessor"/> + <see cref="ICurrentTenant.Change"/>. Otherwise runs
    /// once against the caller's scope.
    /// </summary>
    private async Task<IReadOnlyList<AuditEntry>> CollectAcrossScopesAsync(
        Func<IAuditingDbContext, Task<List<AuditEntry>>> query,
        CancellationToken cancellationToken)
    {
        if (resolver.StorageMode == DualScopeStorageMode.Segregated && !currentTenant.IsAvailable)
        {
            List<AuditEntry> results = [];

            // Host portion first.
            await using (IAuditingDbContext hostCtx = await resolver
                .OpenForScopeAsync(tenantId: null, cancellationToken).ConfigureAwait(false))
            {
                results.AddRange(await query(hostCtx).ConfigureAwait(false));
            }

            IReadOnlyList<(Guid Id, string Name)> tenants = await tenantsAccessor
                .GetAllAsync(cancellationToken).ConfigureAwait(false);
            foreach ((Guid id, string name) in tenants)
            {
                using (currentTenant.Change(id, name))
                await using (IAuditingDbContext tenantCtx = await resolver
                    .OpenForScopeAsync(id, cancellationToken).ConfigureAwait(false))
                {
                    results.AddRange(await query(tenantCtx).ConfigureAwait(false));
                }
            }

            return results;
        }

        await using IAuditingDbContext singleDb = await resolver
            .OpenForScopeAsync(currentTenant.IsAvailable ? currentTenant.Id : null, cancellationToken)
            .ConfigureAwait(false);
        return await query(singleDb).ConfigureAwait(false);
    }

    private static async Task DisposeAllAsync(IReadOnlyList<IAuditingDbContext> contexts)
    {
        foreach (IAuditingDbContext ctx in contexts)
        {
            await ctx.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static readonly MethodInfo AnyMethodInfo = typeof(Enumerable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(Enumerable.Any)
            && m.GetParameters().Length == 2)
        .MakeGenericMethod(typeof(AuditEntityChange));

    /// <summary>
    /// Rewrites every reference to <c>from</c> in an expression tree as <c>to</c>. Lets us
    /// re-use the body of an <c>Expression&lt;Func&lt;T, bool&gt;&gt;</c> under a fresh
    /// parameter when combining per-scope predicates via <c>Expression.OrElse</c>.
    /// </summary>
    private sealed class ParameterRebinder(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        public static Expression Replace(Expression body, ParameterExpression from, ParameterExpression to) =>
            new ParameterRebinder(from, to).Visit(body);

        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }
}
