using Granit.Auditing.Abstractions;
using Granit.Auditing.Domain;
using Granit.Auditing.Options;
using Granit.MultiTenancy;
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
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entry is not null)
        {
            await cache.SetAsync(cacheKey, entry, new FusionCacheEntryOptions { Duration = _options.CacheEntryTtl }, token: cancellationToken).ConfigureAwait(false);
        }

        return entry;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<AuditEntry>> GetPagedAsync(
        AuditingQuery query,
        CancellationToken cancellationToken = default)
    {
        await using AuditingDbContext dbContext = await dbContextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        IQueryable<AuditEntry> queryable = dbContext.AuditEntries
            .Include(e => e.EntityChanges)
            .AsNoTracking();

        queryable = ApplyFilters(queryable, query);

        int totalCount = await queryable.CountAsync(cancellationToken).ConfigureAwait(false);

        List<AuditEntry> items = await queryable
            .OrderByDescending(e => e.Timestamp)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<AuditEntry>(
            items,
            totalCount,
            HasMore: query.Page * query.PageSize < totalCount);
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

        IQueryable<AuditEntry> queryable = dbContext.AuditEntries
            .Include(e => e.EntityChanges)
                .ThenInclude(ec => ec.PropertyChanges)
            .Where(e => e.EntityChanges.Any(ec =>
                ec.EntityType == entityType && ec.EntityId == entityId))
            .AsNoTracking();

        int totalCount = await queryable.CountAsync(cancellationToken).ConfigureAwait(false);

        List<AuditEntry> items = await queryable
            .OrderByDescending(e => e.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        PagedResult<AuditEntry> result = new(
            items,
            totalCount,
            HasMore: page * pageSize < totalCount);

        await cache.SetAsync(cacheKey, result, new FusionCacheEntryOptions { Duration = _options.CacheEntityQueryTtl }, token: cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static IQueryable<AuditEntry> ApplyFilters(
        IQueryable<AuditEntry> queryable,
        AuditingQuery query)
    {
        if (query.UserId is not null)
        {
            queryable = queryable.Where(e => e.UserId == query.UserId);
        }

        if (query.EntityType is not null)
        {
            queryable = queryable.Where(e =>
                e.EntityChanges.Any(ec => ec.EntityType == query.EntityType));
        }

        if (query.EntityId is not null)
        {
            queryable = queryable.Where(e =>
                e.EntityChanges.Any(ec => ec.EntityId == query.EntityId));
        }

        if (query.Category is not null)
        {
            queryable = queryable.Where(e => e.Category == query.Category);
        }

        if (query.From is not null)
        {
            queryable = queryable.Where(e => e.Timestamp >= query.From);
        }

        if (query.To is not null)
        {
            queryable = queryable.Where(e => e.Timestamp < query.To);
        }

        return queryable;
    }
}
