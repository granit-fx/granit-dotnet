using Granit.AuditLog.Abstractions;
using Granit.AuditLog.Domain;
using Granit.AuditLog.Options;
using Granit.MultiTenancy;
using Granit.Querying;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Granit.AuditLog.EntityFrameworkCore.Internal.Services;

/// <summary>
/// EF Core implementation of <see cref="IAuditLogReader"/> with in-memory caching
/// for immutable audit entries and short-lived entity query results.
/// </summary>
internal sealed class EfCoreAuditLogReader(
    IDbContextFactory<AuditLogDbContext> dbContextFactory,
    IMemoryCache memoryCache,
    ICurrentTenant currentTenant,
    IOptions<AuditLogOptions> options) : IAuditLogReader
{
    private readonly AuditLogOptions _options = options.Value;
    private string TenantCachePrefix => currentTenant is { IsAvailable: true, Id: { } id } ? id.ToString() : "global";

    /// <inheritdoc/>
    public async Task<AuditLogEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"audit:{TenantCachePrefix}:entry:{id}";

        if (memoryCache.TryGetValue(cacheKey, out AuditLogEntry? cached))
        {
            return cached;
        }

        await using AuditLogDbContext dbContext = await dbContextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        AuditLogEntry? entry = await dbContext.AuditLogEntries
            .Include(e => e.EntityChanges)
                .ThenInclude(ec => ec.PropertyChanges)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entry is not null)
        {
            memoryCache.Set(cacheKey, entry, _options.CacheEntryTtl);
        }

        return entry;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<AuditLogEntry>> GetPagedAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        await using AuditLogDbContext dbContext = await dbContextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        IQueryable<AuditLogEntry> queryable = dbContext.AuditLogEntries
            .Include(e => e.EntityChanges)
            .AsNoTracking();

        queryable = ApplyFilters(queryable, query);

        int totalCount = await queryable.CountAsync(cancellationToken).ConfigureAwait(false);

        List<AuditLogEntry> items = await queryable
            .OrderByDescending(e => e.Timestamp)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<AuditLogEntry>(
            items,
            totalCount,
            HasMore: query.Page * query.PageSize < totalCount);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<AuditLogEntry>> GetByEntityAsync(
        string entityType,
        string entityId,
        int page = 1,
        int pageSize = QueryingDefaults.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        string cacheKey = $"audit:{TenantCachePrefix}:entity:{entityType}:{entityId}:p{page}:s{pageSize}";

        if (memoryCache.TryGetValue(cacheKey, out PagedResult<AuditLogEntry>? cached))
        {
            return cached!;
        }

        await using AuditLogDbContext dbContext = await dbContextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        IQueryable<AuditLogEntry> queryable = dbContext.AuditLogEntries
            .Include(e => e.EntityChanges)
                .ThenInclude(ec => ec.PropertyChanges)
            .Where(e => e.EntityChanges.Any(ec =>
                ec.EntityType == entityType && ec.EntityId == entityId))
            .AsNoTracking();

        int totalCount = await queryable.CountAsync(cancellationToken).ConfigureAwait(false);

        List<AuditLogEntry> items = await queryable
            .OrderByDescending(e => e.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new PagedResult<AuditLogEntry>(
            items,
            totalCount,
            HasMore: page * pageSize < totalCount);

        memoryCache.Set(cacheKey, result, _options.CacheEntityQueryTtl);
        return result;
    }

    private static IQueryable<AuditLogEntry> ApplyFilters(
        IQueryable<AuditLogEntry> queryable,
        AuditLogQuery query)
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
