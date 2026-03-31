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
/// Registered as Scoped. Uses <see cref="IServiceScopeFactory"/> to create
/// a dedicated scope per database operation, ensuring correct EF Core lifetime.
/// </remarks>
internal sealed class EfCoreReferenceDataStore<TEntity, TDbContext>(
    IServiceScopeFactory scopeFactory,
    IFusionCache cache,
    IOptions<ReferenceDataOptions> options) : IReferenceDataStoreReader<TEntity>, IReferenceDataStoreWriter<TEntity>
    where TEntity : ReferenceDataEntity
    where TDbContext : DbContext
{
    private static readonly string EntityName = typeof(TEntity).Name;
    private static string AllCacheKey => $"refdata:{EntityName}:all";
    private static string CodeCacheKey(string code) => $"refdata:{EntityName}:{code}";

    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IFusionCache _cache = cache;
    private readonly ReferenceDataOptions _options = options.Value;

    /// <inheritdoc/>
    public async Task<PagedResult<TEntity>> GetAllAsync(
        ReferenceDataQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        TDbContext context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        query ??= new ReferenceDataQuery();

        IQueryable<TEntity> queryable = context.Set<TEntity>().AsNoTracking();

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
                EF.Functions.Like(e.LabelCs, $"%{term}%"));
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

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        TDbContext context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        TEntity? entity = await context.Set<TEntity>()
            .AsNoTracking()
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
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        TDbContext context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        // Bypass the Active filter so inactive records are detected, preventing a
        // duplicate-key error when the seeder runs a second time against an existing
        // (possibly inactive) entry with the same Code.
        bool exists = await context.Set<TEntity>()
            .IgnoreQueryFilters([GranitFilterNames.Active])
            .AnyAsync(e => e.Code == entity.Code, cancellationToken)
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
                .IgnoreQueryFilters([GranitFilterNames.Active])
                .AnyAsync(e => e.Code == entity.Code, cancellationToken)
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
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        TDbContext context = scope.ServiceProvider.GetRequiredService<TDbContext>();

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
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        TDbContext context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        // Bypass the Active filter so inactive records can be found and reactivated.
        TEntity? entity = await context.Set<TEntity>()
            .IgnoreQueryFilters([GranitFilterNames.Active])
            .FirstOrDefaultAsync(e => e.Code == code, cancellationToken).ConfigureAwait(false);

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
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        TDbContext context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        List<TEntity> children = await context.Set<TEntity>()
            .AsNoTracking()
            .Where(e => e.ParentCode == parentCode && e.IsActive)
            .OrderBy(e => e.SortOrder)
            .ThenBy(e => e.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return children;
    }

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
