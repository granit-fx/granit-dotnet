using Granit.Identity.Federated.Domain;
using Granit.Identity.Federated.EntityFrameworkCore.DbContext;
using Granit.Identity.Federated.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.Federated.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IUserCacheStore"/>.
/// All read operations use <c>AsNoTracking</c> for performance.
/// </summary>
internal sealed class EfCoreUserCacheStore<TContext>(TContext context, IUserLookupHasher hasher)
    : IUserCacheStore
    where TContext : Microsoft.EntityFrameworkCore.DbContext, IUserCacheDbContext
{
    // -- Read --

    public Task<UserCacheEntry?> FindByExternalIdAsync(
        string externalUserId, Guid? tenantId, CancellationToken cancellationToken = default) =>
        context.UserCacheEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.TenantId == tenantId && e.ExternalUserId == externalUserId,
                cancellationToken);

    public Task<UserCacheEntry?> FindFirstByExternalIdAsync(
        string externalUserId, CancellationToken cancellationToken = default) =>
        context.UserCacheEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.ExternalUserId == externalUserId,
                cancellationToken);

    public async Task<IReadOnlyList<UserCacheEntry>> FindByExternalIdsAsync(
        IReadOnlyCollection<string> externalUserIds, Guid? tenantId, CancellationToken cancellationToken = default) =>
        await context.UserCacheEntries
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && externalUserIds.Contains(e.ExternalUserId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<(IReadOnlyList<UserCacheEntry> Items, int TotalCount)> SearchAsync(
        string term, Guid? tenantId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        // Encrypted columns can't be LIKE-scanned. Admin search now only supports
        // exact-match on email, resolved via the lookup hash. Anything else returns
        // empty — the admin UI should direct operators to enter a full email.
        if (string.IsNullOrWhiteSpace(term) || !term.Contains('@'))
        {
            return ([], 0);
        }

        string? hash = hasher.ComputeEmailHash(term);
        if (hash is null)
        {
            return ([], 0);
        }

        IQueryable<UserCacheEntry> query = context.UserCacheEntries
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.EmailHash == hash);

        int totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        int skip = (page - 1) * pageSize;
        List<UserCacheEntry> items = await query
            .OrderBy(e => e.ExternalUserId)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return (items, totalCount);
    }

    // -- Diagnostics --

    public Task<int> GetCountAsync(Guid? tenantId, CancellationToken cancellationToken = default) =>
        context.UserCacheEntries
            .AsNoTracking()
            .CountAsync(e => e.TenantId == tenantId, cancellationToken);

    public Task<int> GetStaleCountAsync(
        Guid? tenantId, DateTimeOffset threshold, CancellationToken cancellationToken = default) =>
        context.UserCacheEntries
            .AsNoTracking()
            .CountAsync(e => e.TenantId == tenantId && e.LastSyncedAt < threshold, cancellationToken);

    public async Task<IReadOnlyList<string>> FindStaleExternalIdsAsync(
        Guid? tenantId, DateTimeOffset threshold, int batchSize, CancellationToken cancellationToken = default) =>
        await context.UserCacheEntries
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.LastSyncedAt < threshold)
            .OrderBy(e => e.LastSyncedAt)
            .Take(batchSize)
            .Select(e => e.ExternalUserId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<(DateTimeOffset? Oldest, DateTimeOffset? Newest)> GetSyncRangeAsync(
        Guid? tenantId, CancellationToken cancellationToken = default)
    {
        IQueryable<UserCacheEntry> query = context.UserCacheEntries
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId);

        if (!await query.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return (null, null);
        }

        DateTimeOffset oldest = await query.MinAsync(e => e.LastSyncedAt, cancellationToken).ConfigureAwait(false);
        DateTimeOffset newest = await query.MaxAsync(e => e.LastSyncedAt, cancellationToken).ConfigureAwait(false);

        return (oldest, newest);
    }

    // -- Write --

    public async Task UpsertAsync(UserCacheEntry entry, CancellationToken cancellationToken = default)
    {
        // Keep EmailHash in sync with Email so admin search finds the row. Callers
        // may pre-compute this (CachedUserLookupService does), but the store also
        // recomputes defensively so direct consumers of UpsertAsync stay correct.
        entry.EmailHash = hasher.ComputeEmailHash(entry.Email);

        UserCacheEntry? existing = await context.UserCacheEntries
            .FirstOrDefaultAsync(
                e => e.TenantId == entry.TenantId && e.ExternalUserId == entry.ExternalUserId,
                cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            context.UserCacheEntries.Add(entry);
        }
        else
        {
            existing.Username = entry.Username;
            existing.Email = entry.Email;
            existing.EmailHash = entry.EmailHash;
            existing.FirstName = entry.FirstName;
            existing.LastName = entry.LastName;
            existing.Enabled = entry.Enabled;
            existing.LastSyncedAt = entry.LastSyncedAt;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertManyAsync(
        IReadOnlyList<UserCacheEntry> entries, CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
        {
            return;
        }

        // Compute hashes up front so we don't hit the hasher per-entry under lock.
        foreach (UserCacheEntry entry in entries)
        {
            entry.EmailHash = hasher.ComputeEmailHash(entry.Email);
        }

        var externalIds = entries.Select(e => e.ExternalUserId).ToHashSet();
        Guid? tenantId = entries[0].TenantId;

        Dictionary<string, UserCacheEntry> existingEntries = await context.UserCacheEntries
            .Where(e => e.TenantId == tenantId && externalIds.Contains(e.ExternalUserId))
            .ToDictionaryAsync(e => e.ExternalUserId, cancellationToken).ConfigureAwait(false);

        foreach (UserCacheEntry entry in entries)
        {
            if (existingEntries.TryGetValue(entry.ExternalUserId, out UserCacheEntry? existing))
            {
                existing.Username = entry.Username;
                existing.Email = entry.Email;
                existing.EmailHash = entry.EmailHash;
                existing.FirstName = entry.FirstName;
                existing.LastName = entry.LastName;
                existing.Enabled = entry.Enabled;
                existing.LastSyncedAt = entry.LastSyncedAt;
            }
            else
            {
                context.UserCacheEntries.Add(entry);
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    // -- GDPR --

    public async Task DeleteByExternalIdAsync(
        string externalUserId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        List<UserCacheEntry> entries = await context.UserCacheEntries
            .Where(e => e.TenantId == tenantId && e.ExternalUserId == externalUserId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        context.UserCacheEntries.RemoveRange(entries);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAllByTenantAsync(Guid? tenantId, CancellationToken cancellationToken = default)
    {
        List<UserCacheEntry> entries = await context.UserCacheEntries
            .Where(e => e.TenantId == tenantId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        context.UserCacheEntries.RemoveRange(entries);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task PseudonymizeAsync(
        string externalUserId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        UserCacheEntry? entry = await context.UserCacheEntries
            .FirstOrDefaultAsync(
                e => e.TenantId == tenantId && e.ExternalUserId == externalUserId,
                cancellationToken).ConfigureAwait(false);

        if (entry is null)
        {
            return;
        }

        entry.Username = "anonymized";
        entry.Email = "anonymized@anonymized.local";
        // Null the hash — pseudonymised rows should not be discoverable via email
        // lookup, and every pseudonymised row sharing the same constant email would
        // otherwise collide on the same hash (harmless, but noisy in the index).
        entry.EmailHash = null;
        entry.FirstName = "Anonymized";
        entry.LastName = "User";
        entry.Enabled = false;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
