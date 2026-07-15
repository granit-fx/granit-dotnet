using Granit.Identity.Federated.Domain;
using Granit.Identity.Federated.Internal;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.Federated.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IUserCacheStore"/>. Each operation opens its own
/// <see cref="IdentityFederatedDbContext"/> via the injected factory.
/// </summary>
/// <remarks>
/// All read operations use <c>AsNoTracking</c> for performance.
/// </remarks>
internal sealed class EfCoreUserCacheStore(
    IDbContextFactory<IdentityFederatedDbContext> contextFactory,
    IUserLookupHasher hasher) : IUserCacheStore
{
    // -- Read --

    public async Task<FederatedIdentity?> FindByExternalIdAsync(
        string externalUserId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        await using IdentityFederatedDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await db.FederatedIdentities
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.TenantId == tenantId && e.ExternalUserId == externalUserId,
                cancellationToken).ConfigureAwait(false);
    }

    public async Task<FederatedIdentity?> FindFirstByExternalIdAsync(
        string externalUserId, CancellationToken cancellationToken = default)
    {
        await using IdentityFederatedDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Host-context lookup, documented "regardless of tenant": the multi-tenant filter
        // must be bypassed explicitly — with no ambient tenant it would otherwise hide every
        // tenant-scoped mirror, and the cache-aside caller would re-insert a duplicate row.
        return await db.FederatedIdentities
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ExternalUserId == externalUserId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<FederatedIdentity>> FindByExternalIdsAsync(
        IReadOnlyCollection<string> externalUserIds, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        await using IdentityFederatedDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await db.FederatedIdentities
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && externalUserIds.Contains(e.ExternalUserId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<FederatedIdentity> Items, int TotalCount)> SearchAsync(
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

        await using IdentityFederatedDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        IQueryable<FederatedIdentity> query = db.FederatedIdentities
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.EmailHash == hash);

        int totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        int skip = (page - 1) * pageSize;
        List<FederatedIdentity> items = await query
            .OrderBy(e => e.ExternalUserId)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return (items, totalCount);
    }

    // -- Diagnostics --

    public async Task<int> GetCountAsync(Guid? tenantId, CancellationToken cancellationToken = default)
    {
        await using IdentityFederatedDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await db.FederatedIdentities
            .AsNoTracking()
            .CountAsync(e => e.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> GetStaleCountAsync(
        Guid? tenantId, DateTimeOffset threshold, CancellationToken cancellationToken = default)
    {
        await using IdentityFederatedDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await db.FederatedIdentities
            .AsNoTracking()
            .CountAsync(e => e.TenantId == tenantId && e.LastSyncedAt < threshold, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> FindStaleExternalIdsAsync(
        Guid? tenantId, DateTimeOffset threshold, int batchSize, CancellationToken cancellationToken = default)
    {
        await using IdentityFederatedDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await db.FederatedIdentities
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.LastSyncedAt < threshold)
            .OrderBy(e => e.LastSyncedAt)
            .Take(batchSize)
            .Select(e => e.ExternalUserId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(DateTimeOffset? Oldest, DateTimeOffset? Newest)> GetSyncRangeAsync(
        Guid? tenantId, CancellationToken cancellationToken = default)
    {
        await using IdentityFederatedDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        IQueryable<FederatedIdentity> query = db.FederatedIdentities
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

    public async Task<Guid> UpsertAsync(FederatedIdentity entry, CancellationToken cancellationToken = default)
    {
        // Keep EmailHash in sync with Email so admin search finds the row. Callers
        // may pre-compute this (CachedUserLookupService does), but the store also
        // recomputes defensively so direct consumers of UpsertAsync stay correct.
        entry.EmailHash = hasher.ComputeEmailHash(entry.Email);

        // Upsert with one retry: two concurrent first-logins of the same (TenantId, ExternalUserId)
        // — e.g. the same user hitting two pods — both miss the existing row and both insert,
        // tripping the unique index. On that conflict, re-read and update the row the winner
        // created instead of surfacing the DbUpdateException. Provider-agnostic (no ON CONFLICT).
        // Returns the persisted row's Id so the caller can detect a lost insert race (persisted Id
        // != entry.Id) and compensate — FederatedIdentityWriter deletes the orphaned canonical
        // User it created for its losing insert. The second attempt's own DbUpdateException is not
        // caught (filter is attempt == 0), so a genuine failure still surfaces and bounds the loop.
        for (int attempt = 0; attempt <= 1; attempt++)
        {
            await using IdentityFederatedDbContext db = await contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            FederatedIdentity? existing = await db.FederatedIdentities
                .FirstOrDefaultAsync(
                    e => e.TenantId == entry.TenantId && e.ExternalUserId == entry.ExternalUserId,
                    cancellationToken).ConfigureAwait(false);

            if (existing is null)
            {
                db.FederatedIdentities.Add(entry);
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

            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return existing?.Id ?? entry.Id;
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                // Lost an insert race; loop once to re-read and update the winner's row.
            }
        }

        // Unreachable: attempt 1 either returns or rethrows its own DbUpdateException.
        throw new InvalidOperationException("UpsertAsync retry loop exited without persisting.");
    }

    public async Task UpsertManyAsync(
        IReadOnlyList<FederatedIdentity> entries, CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
        {
            return;
        }

        // Compute hashes up front so we don't hit the hasher per-entry under lock.
        foreach (FederatedIdentity entry in entries)
        {
            entry.EmailHash = hasher.ComputeEmailHash(entry.Email);
        }

        HashSet<string> externalIds = [.. entries.Select(e => e.ExternalUserId)];
        Guid? tenantId = entries[0].TenantId;

        await using IdentityFederatedDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        Dictionary<string, FederatedIdentity> existingEntries = await db.FederatedIdentities
            .Where(e => e.TenantId == tenantId && externalIds.Contains(e.ExternalUserId))
            .ToDictionaryAsync(e => e.ExternalUserId, cancellationToken).ConfigureAwait(false);

        foreach (FederatedIdentity entry in entries)
        {
            if (existingEntries.TryGetValue(entry.ExternalUserId, out FederatedIdentity? existing))
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
                db.FederatedIdentities.Add(entry);
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    // -- GDPR --

    public async Task<int> DeleteByExternalIdAsync(
        string externalUserId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        await using IdentityFederatedDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Null tenant scope = GDPR Art. 17 sweep across ALL partitions (contract of
        // IdentityUserDeletedEto): the multi-tenant filter would otherwise translate the
        // intent into "host rows only" and leave tenant mirrors behind. A scoped delete
        // keeps the filter active as a guard — the caller must have established a matching
        // ambient tenant for the row to be visible.
        IQueryable<FederatedIdentity> query = tenantId is null
            ? db.FederatedIdentities
                .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
                .Where(e => e.ExternalUserId == externalUserId)
            : db.FederatedIdentities
                .Where(e => e.TenantId == tenantId && e.ExternalUserId == externalUserId);

        List<FederatedIdentity> entries = await query
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        db.FederatedIdentities.RemoveRange(entries);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entries.Count;
    }

    public async Task DeleteAllByTenantAsync(Guid? tenantId, CancellationToken cancellationToken = default)
    {
        await using IdentityFederatedDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<FederatedIdentity> entries = await db.FederatedIdentities
            .Where(e => e.TenantId == tenantId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        db.FederatedIdentities.RemoveRange(entries);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task PseudonymizeAsync(
        string externalUserId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        await using IdentityFederatedDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        FederatedIdentity? entry = await db.FederatedIdentities
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

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
