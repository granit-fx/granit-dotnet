using Granit.Authentication.ApiKeys.Domain;
using Granit.Querying;
using Microsoft.EntityFrameworkCore;

namespace Granit.Authentication.ApiKeys.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IApiKeyAdminStore"/> using
/// <see cref="IDbContextFactory{TContext}"/> for safe concurrent access.
/// </summary>
internal sealed class EfCoreApiKeyAdminStore(
    IDbContextFactory<ApiKeysDbContext> contextFactory) : IApiKeyAdminStore
{
    /// <inheritdoc/>
    public async Task<ApiKeyEntry?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using ApiKeysDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await db.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<ApiKeyEntry>> ListAsync(
        string? search = null,
        ApiKeyType? type = null,
        string? environment = null,
        bool includeRevoked = false,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        await using ApiKeysDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        IQueryable<ApiKeyEntry> query = db.ApiKeys.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(k => k.Name.Contains(search));
        }

        if (type.HasValue)
        {
            query = query.Where(k => k.Type == type.Value);
        }

        if (!string.IsNullOrWhiteSpace(environment))
        {
            query = query.Where(k => k.Environment == environment);
        }

        if (!includeRevoked)
        {
            query = query.Where(k => k.RevokedAt == null);
        }

        int totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        List<ApiKeyEntry> items = await query
            .OrderByDescending(k => k.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<ApiKeyEntry>(items, totalCount, HasMore: (page - 1) * pageSize + items.Count < totalCount);
    }

    /// <inheritdoc/>
    public async Task CreateAsync(ApiKeyEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await using ApiKeysDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        db.ApiKeys.Add(entry);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> RevokeAsync(Guid id, DateTimeOffset revokedAt, CancellationToken cancellationToken = default)
    {
        await using ApiKeysDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        int updated = await db.ApiKeys
            .Where(k => k.Id == id && k.RevokedAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(k => k.RevokedAt, revokedAt),
                cancellationToken)
            .ConfigureAwait(false);

        return updated > 0;
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateScopesAsync(
        Guid id,
        List<string> permissions,
        List<string> allowedCidrs,
        CancellationToken cancellationToken = default)
    {
        await using ApiKeysDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        ApiKeyEntry? entry = await db.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entry is null)
        {
            return false;
        }

        entry.UpdatePermissions(permissions);
        entry.UpdateAllowedCidrs(allowedCidrs);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }
}
