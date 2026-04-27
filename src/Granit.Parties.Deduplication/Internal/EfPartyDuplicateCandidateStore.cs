using Granit.MultiTenancy;
using Granit.Parties.Deduplication.Domain;
using Granit.Parties.EntityFrameworkCore.Deduplication;
using Granit.Parties.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;

namespace Granit.Parties.Deduplication.Internal;

/// <summary>
/// EF-backed implementation of <see cref="IPartyDuplicateCandidateStore"/>: reads + dismisses
/// rows of <c>parties_duplicate_candidates</c> via <see cref="PartiesDbContext"/>.
/// Tenant-scoped: every query applies an explicit <c>WHERE tenant_id = currentTenant.Id</c>
/// (or <c>IS NULL</c>) so the store is safe to use from non-HTTP contexts where the
/// ambient <c>IMultiTenant</c> filter may not be set the way HTTP middleware would set it.
/// </summary>
internal sealed class EfPartyDuplicateCandidateStore(
    IDbContextFactory<PartiesDbContext> contextFactory,
    ICurrentTenant currentTenant,
    IClock clock) : IPartyDuplicateCandidateStore
{
    private const int MaxPageSize = 200;

    public async Task<DuplicateCandidatePage> ListAsync(
        DuplicateMatchTier? tier,
        decimal? minScore,
        bool includeDismissed,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        await using PartiesDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        IQueryable<PartyDuplicateCandidate> query = ScopedToTenant(db);

        if (tier is { } t)
        {
            int tierKey = (int)t;
            query = query.Where(c => c.Tier == tierKey);
        }

        if (minScore is { } score)
        {
            query = query.Where(c => c.Score >= score);
        }

        if (!includeDismissed)
        {
            query = query.Where(c => c.DismissedAt == null);
        }

        int total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        List<PartyDuplicateCandidate> items = await query
            .OrderByDescending(c => c.Score)
            .ThenByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new DuplicateCandidatePage(items, total, page, pageSize);
    }

    public async Task<PartyDuplicateCandidate?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await using PartiesDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await ScopedToTenant(db)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PartyDuplicateCandidate>> ListForPartyAsync(
        Guid partyId,
        CancellationToken cancellationToken)
    {
        await using PartiesDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await ScopedToTenant(db)
            .Where(c => c.DismissedAt == null
                && (c.PartyId == partyId || c.CandidateId == partyId))
            .OrderByDescending(c => c.Score)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DismissAsync(Guid id, CancellationToken cancellationToken)
    {
        await using PartiesDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        PartyDuplicateCandidate? row = await ScopedToTenant(db)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (row is null || row.DismissedAt is not null)
        {
            return false;
        }

        row.Dismiss(clock.Now);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Applies the explicit tenant filter and bypasses the ambient
    /// <c>IMultiTenant</c> query filter — robust regardless of caller context.</summary>
    private IQueryable<PartyDuplicateCandidate> ScopedToTenant(PartiesDbContext db)
    {
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        return db.DuplicateCandidates
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .Where(c => c.TenantId == tenantId);
    }
}
