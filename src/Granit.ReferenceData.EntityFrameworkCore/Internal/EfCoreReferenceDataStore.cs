using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Granit.ReferenceData.Domain;
using Granit.ReferenceData.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.ReferenceData.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IReferenceDataStoreReader{TEntity}"/> and <see cref="IReferenceDataStoreWriter{TEntity}"/>.
/// Persists reference data in the host application's DbContext with built-in
/// <see cref="IFusionCache"/> for read-heavy workloads.
/// </summary>
/// <typeparam name="TEntity">The concrete reference data entity type.</typeparam>
/// <typeparam name="TDbContext">The host application's DbContext.</typeparam>
/// <remarks>
/// <para>
/// Registered as Scoped. Uses <see cref="IServiceScopeFactory"/> to create
/// a dedicated scope per database operation, ensuring correct EF Core lifetime.
/// </para>
/// <para>
/// Multi-tenancy behavior depends on <see cref="ReferenceDataScope"/>:
/// <list type="bullet">
/// <item><description><see cref="ReferenceDataScope.Global"/>: bypasses the MultiTenant query filter
/// and explicitly filters on <c>TenantId IS NULL</c>. Writes neutralize the tenant context
/// via <see cref="ICurrentTenant.Change"/> to prevent the interceptor from injecting a TenantId.</description></item>
/// <item><description><see cref="ReferenceDataScope.Tenant"/>: relies on the standard MultiTenant query filter
/// for read isolation. The interceptor auto-injects the current tenant's ID on writes.</description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class EfCoreReferenceDataStore<TEntity, TDbContext>(
    IServiceScopeFactory scopeFactory,
    IFusionCache cache,
    IOptions<ReferenceDataOptions> options,
    ReferenceDataScope scope,
    ICurrentTenant currentTenant,
    IGuidGenerator guidGenerator) : IReferenceDataStoreReader<TEntity>, IReferenceDataStoreWriter<TEntity>
    where TEntity : ReferenceDataEntity
    where TDbContext : DbContext
{
    private static readonly string EntityName = typeof(TEntity).Name;

    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IFusionCache _cache = cache;
    private readonly ReferenceDataOptions _options = options.Value;
    private readonly ReferenceDataScope _scope = scope;
    private readonly ICurrentTenant _currentTenant = currentTenant;
    private readonly IGuidGenerator _guidGenerator = guidGenerator;

    private string AllCacheKey => _scope == ReferenceDataScope.Global
        ? $"refdata:{EntityName}:host:all"
        : $"refdata:{EntityName}:t:{_currentTenant.Id}:all";

    private string CodeCacheKey(string code) => _scope == ReferenceDataScope.Global
        ? $"refdata:{EntityName}:host:{code}"
        : $"refdata:{EntityName}:t:{_currentTenant.Id}:{code}";

    /// <inheritdoc/>
    public async Task<PagedResult<TEntity>> GetAllAsync(
        ReferenceDataQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope serviceScope = _scopeFactory.CreateAsyncScope();
        TDbContext context = serviceScope.ServiceProvider.GetRequiredService<TDbContext>();

        query ??= new ReferenceDataQuery();

        IQueryable<TEntity> queryable = context.Set<TEntity>().AsNoTracking();

        // Scope-aware tenant filtering
        if (_scope == ReferenceDataScope.Global)
        {
            // Global data has TenantId=null; bypass the auto MultiTenant filter
            // and explicitly filter for null TenantId.
            queryable = queryable
                .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
                .Where(e => e.TenantId == null);
        }
        // Tenant scope: the standard MultiTenant query filter handles isolation automatically.

        // Active filter
        if (query.ActiveOnly)
        {
            queryable = queryable.Where(e => e.IsActive);
        }

        // Search filter (Code or any label, case-insensitive)
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            string term = EscapeLikePattern(query.SearchTerm);
            queryable = queryable.Where(e =>
                EF.Functions.Like(e.Code, $"%{term}%") ||
                EF.Functions.Like(e.LabelEn, $"%{term}%") ||
                EF.Functions.Like(e.LabelFr, $"%{term}%") ||
                EF.Functions.Like(e.LabelNl, $"%{term}%") ||
                EF.Functions.Like(e.LabelDe, $"%{term}%") ||
                EF.Functions.Like(e.LabelEs, $"%{term}%") ||
                EF.Functions.Like(e.LabelIt, $"%{term}%") ||
                EF.Functions.Like(e.LabelPt, $"%{term}%") ||
                EF.Functions.Like(e.LabelZh, $"%{term}%") ||
                EF.Functions.Like(e.LabelJa, $"%{term}%") ||
                EF.Functions.Like(e.LabelPl, $"%{term}%") ||
                EF.Functions.Like(e.LabelTr, $"%{term}%") ||
                EF.Functions.Like(e.LabelKo, $"%{term}%") ||
                EF.Functions.Like(e.LabelSv, $"%{term}%") ||
                EF.Functions.Like(e.LabelCs, $"%{term}%") ||
                EF.Functions.Like(e.LabelHi, $"%{term}%"));
        }

        // Total count before pagination
        int totalCount = await queryable.CountAsync(cancellationToken).ConfigureAwait(false);

        // Sorting
        queryable = query.SortBy?.ToUpperInvariant() switch
        {
            "CODE" => query.Descending
                ? queryable.OrderByDescending(e => e.Code)
                : queryable.OrderBy(e => e.Code),
            "LABEL" => query.Descending
                ? queryable.OrderByDescending(e => e.LabelEn)
                : queryable.OrderBy(e => e.LabelEn),
            _ => query.Descending
                ? queryable.OrderByDescending(e => e.SortOrder).ThenBy(e => e.Code)
                : queryable.OrderBy(e => e.SortOrder).ThenBy(e => e.Code),
        };

        // Pagination
        int clampedPage = Math.Max(query.Page, 1);
        int clampedPageSize = Math.Clamp(query.PageSize, 1, QueryEngineDefaults.MaxPageSize);
        int skip = (clampedPage - 1) * clampedPageSize;

        queryable = queryable.Skip(skip).Take(clampedPageSize);

        List<TEntity> items = await queryable.ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PagedResult<TEntity>(items, totalCount, HasMore: skip + items.Count < totalCount);
    }

    /// <inheritdoc/>
    public async Task<TEntity?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        string cacheKey = CodeCacheKey(code);

        MaybeValue<TEntity?> maybe = await _cache.TryGetAsync<TEntity?>(cacheKey, token: cancellationToken).ConfigureAwait(false);
        if (maybe.HasValue)
        {
            return maybe.Value;
        }

        await using AsyncServiceScope serviceScope = _scopeFactory.CreateAsyncScope();
        TDbContext context = serviceScope.ServiceProvider.GetRequiredService<TDbContext>();

        IQueryable<TEntity> queryable = context.Set<TEntity>().AsNoTracking();

        if (_scope == ReferenceDataScope.Global)
        {
            queryable = queryable
                .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
                .Where(e => e.TenantId == null);
        }

        TEntity? entity = await queryable
            .FirstOrDefaultAsync(e => e.Code == code, cancellationToken).ConfigureAwait(false);

        if (entity is not null)
        {
            await _cache.SetAsync(cacheKey, entity, new FusionCacheEntryOptions { Duration = _options.CacheTimeToLive }, token: cancellationToken).ConfigureAwait(false);
        }

        return entity;
    }

    /// <inheritdoc/>
    public async Task CreateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        // Ensure a sequential GUID is assigned when callers (e.g. seeders) don't set one.
        if (entity.Id == Guid.Empty)
        {
            entity.Id = _guidGenerator.Create();
        }

        // For Global scope, neutralize the tenant context so that AuditedEntityInterceptor
        // does not auto-inject a TenantId. TenantId must remain null for global entries.
        using IDisposable? tenantOverride = _scope == ReferenceDataScope.Global
            ? _currentTenant.Change(null)
            : null;

        await using AsyncServiceScope serviceScope = _scopeFactory.CreateAsyncScope();
        TDbContext context = serviceScope.ServiceProvider.GetRequiredService<TDbContext>();

        // Bypass Active and MultiTenant filters so inactive and cross-tenant records are
        // detected, preventing a duplicate-key error when the seeder runs a second time.
        bool exists = await context.Set<TEntity>()
            .IgnoreQueryFilters([GranitFilterNames.Active, GranitFilterNames.MultiTenant])
            .AnyAsync(DuplicateCheck(entity.Code), cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            return;
        }

        context.Set<TEntity>().Add(entity);

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // A concurrent seed may have inserted the same Code between our AnyAsync
            // check and the INSERT (TOCTOU race). Verify with a fresh context — the
            // original DbContext is unusable after a failed SaveChangesAsync.
            await using AsyncServiceScope verifyScope = _scopeFactory.CreateAsyncScope();
            TDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<TDbContext>();

            bool concurrentInsert = await verifyContext.Set<TEntity>()
                .IgnoreQueryFilters([GranitFilterNames.Active, GranitFilterNames.MultiTenant])
                .AnyAsync(DuplicateCheck(entity.Code), cancellationToken)
                .ConfigureAwait(false);

            if (!concurrentInsert)
            {
                throw;
            }

            // Concurrent insert won the race — treat as idempotent success.
            InvalidateCache(entity.Code);
            return;
        }

        InvalidateCache(entity.Code);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope serviceScope = _scopeFactory.CreateAsyncScope();
        TDbContext context = serviceScope.ServiceProvider.GetRequiredService<TDbContext>();

        context.Set<TEntity>().Update(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        InvalidateCache(entity.Code);
    }

    /// <inheritdoc/>
    public async Task SetActiveAsync(
        string code,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope serviceScope = _scopeFactory.CreateAsyncScope();
        TDbContext context = serviceScope.ServiceProvider.GetRequiredService<TDbContext>();

        // Bypass Active filter so inactive records can be found and reactivated.
        // Also bypass MultiTenant for Global scope where no tenant context is active.
        IQueryable<TEntity> queryable = context.Set<TEntity>()
            .IgnoreQueryFilters([GranitFilterNames.Active, GranitFilterNames.MultiTenant]);

        TEntity? entity = _scope == ReferenceDataScope.Global
            ? await queryable.FirstOrDefaultAsync(e => e.Code == code && e.TenantId == null, cancellationToken).ConfigureAwait(false)
            : await queryable.FirstOrDefaultAsync(e => e.Code == code && e.TenantId == _currentTenant.Id, cancellationToken).ConfigureAwait(false);

        if (entity is not null)
        {
            entity.IsActive = isActive;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        InvalidateCache(code);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TEntity>> GetChildrenAsync(
        string? parentCode,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope serviceScope = _scopeFactory.CreateAsyncScope();
        TDbContext context = serviceScope.ServiceProvider.GetRequiredService<TDbContext>();

        IQueryable<TEntity> queryable = context.Set<TEntity>().AsNoTracking();

        if (_scope == ReferenceDataScope.Global)
        {
            queryable = queryable
                .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
                .Where(e => e.TenantId == null);
        }

        List<TEntity> children = await queryable
            .Where(e => e.ParentCode == parentCode && e.IsActive)
            .OrderBy(e => e.SortOrder)
            .ThenBy(e => e.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return children;
    }

    /// <summary>
    /// Returns a scope-aware duplicate predicate: for Global checks Code only (TenantId=null),
    /// for Tenant checks Code + current TenantId.
    /// </summary>
    private System.Linq.Expressions.Expression<Func<TEntity, bool>> DuplicateCheck(string code) =>
        _scope == ReferenceDataScope.Global
            ? e => e.Code == code && e.TenantId == null
            : e => e.Code == code && e.TenantId == _currentTenant.Id;

    private void InvalidateCache(string code)
    {
        _cache.Expire(CodeCacheKey(code));
        _cache.Expire(AllCacheKey);
    }

    /// <summary>
    /// Escapes LIKE-special characters (<c>%</c>, <c>_</c>, <c>[</c>) to prevent
    /// wildcard injection in search terms.
    /// </summary>
    private static string EscapeLikePattern(string input) =>
        input.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
}
