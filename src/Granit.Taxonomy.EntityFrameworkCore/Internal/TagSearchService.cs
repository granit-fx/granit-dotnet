using Granit.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Granit.Taxonomy.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core-backed implementation of <see cref="ITagSearchService"/>. Single-query
/// cross-entity search per ADR-054 — joins tags + assignments and groups by
/// <c>TargetType</c>.
/// </summary>
internal sealed class TagSearchService(
    IDbContextFactory<TaxonomyDbContext> contextFactory,
    ICurrentTenant currentTenant) : ITagSearchService
{
    /// <inheritdoc />
    public async Task<TagSearchResult> SearchAsync(
        string query,
        string scope,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        if (skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skip), "Skip must be non-negative.");
        }
        if (take is <= 0 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "Take must be in (0, 500].");
        }

        await using TaxonomyDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        string prefix = query.Trim() + "%";
        bool crossScope = string.Equals(scope, "*", StringComparison.Ordinal);

        IQueryable<Domain.Tag> tagQuery = context.Tags
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .Where(t => EF.Functions.Like(t.Name, prefix));
        if (!crossScope)
        {
            tagQuery = tagQuery.Where(t => t.Scope == scope);
        }

        int totalCount = await tagQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        List<TagSearchTag> matchingTags = await tagQuery
            .OrderBy(t => t.Name)
            .Skip(skip)
            .Take(take)
            .Select(t => new TagSearchTag(t.Id, t.Name, t.Color, t.Scope))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (matchingTags.Count == 0)
        {
            return new TagSearchResult(
                matchingTags,
                new Dictionary<string, IReadOnlyList<TagSearchHit>>(StringComparer.Ordinal),
                totalCount,
                skip,
                take);
        }

        Guid[] tagIds = [.. matchingTags.Select(t => t.Id)];

        // Single query for every assignment of the matching tags. Grouping happens
        // server-side by (TargetType, TargetId) so the wire shape carries the tag
        // ids per target without N+1 round-trips.
        var rawHits = await context.TagAssignments
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && tagIds.Contains(a.TagId))
            .Select(a => new { a.TargetType, a.TargetId, a.TagId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var grouped = rawHits
            .GroupBy(r => r.TargetType, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<TagSearchHit>)g
                    .GroupBy(r => r.TargetId)
                    .Select(targetGroup => new TagSearchHit(
                        targetGroup.Key,
                        [.. targetGroup.Select(r => r.TagId)]))
                    .OrderBy(h => h.TargetId)
                    .ToList(),
                StringComparer.Ordinal);

        return new TagSearchResult(matchingTags, grouped, totalCount, skip, take);
    }
}
