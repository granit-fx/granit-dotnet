using Granit.Authentication.ApiKeys.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Authentication.ApiKeys.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IApiKeyAdminStore"/> using
/// <see cref="IDbContextFactory{TContext}"/> for safe concurrent access.
/// </summary>
internal sealed class EfCoreApiKeyAdminStore(
    IDbContextFactory<AuthenticationApiKeysDbContext> contextFactory)
    : EfStoreBase<ApiKeyEntry, AuthenticationApiKeysDbContext>(contextFactory), IApiKeyAdminStore
{
    /// <inheritdoc/>
    public new Task<ApiKeyEntry?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        base.FindByIdAsync(id, cancellationToken);

    /// <inheritdoc/>
    public Task<PagedResult<ApiKeyEntry>> ListAsync(
        string? search = null,
        ApiKeyType? type = null,
        string? environment = null,
        bool includeRevoked = false,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        ReadAsync(async db =>
        {
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
        }, cancellationToken);

    /// <inheritdoc/>
    public Task CreateAsync(ApiKeyEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return AddAsync(entry, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> RevokeAsync(Guid id, DateTimeOffset revokedAt, CancellationToken cancellationToken = default) =>
        WriteAsync<bool>(async db =>
        {
            ApiKeyEntry? entry = await db.ApiKeys
                .FirstOrDefaultAsync(k => k.Id == id && k.RevokedAt == null, cancellationToken)
                .ConfigureAwait(false);

            if (entry is null)
            {
                return false;
            }

            entry.Revoke(revokedAt);
            return true;
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> UpdateScopesAsync(
        Guid id,
        List<string> permissions,
        List<string> allowedCidrs,
        CancellationToken cancellationToken = default) =>
        WriteAsync<bool>(async db =>
        {
            ApiKeyEntry? entry = await db.ApiKeys
                .FirstOrDefaultAsync(k => k.Id == id, cancellationToken)
                .ConfigureAwait(false);

            if (entry is null)
            {
                return false;
            }

            entry.UpdatePermissions(permissions);
            entry.UpdateAllowedCidrs(allowedCidrs);
            return true;
        }, cancellationToken);
}
