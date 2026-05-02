using Granit.Activities.Abstractions;
using Granit.Activities.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Activities.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IActivityReader"/>. Tenant filtering is
/// applied automatically by the DbContext's standard query filters — callers
/// never need to constrain by <c>TenantId</c> directly.
/// </summary>
internal sealed class EfCoreActivityReader(IDbContextFactory<ActivitiesDbContext> contextFactory) : IActivityReader
{
    public async Task<Activity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using ActivitiesDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Activities.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Activity>> ListAsync(
        ActivityListFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        await using ActivitiesDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await Apply(context.Activities.AsNoTracking(), filter)
            .OrderBy(a => a.DueAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> CountAsync(ActivityListFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        await using ActivitiesDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await Apply(context.Activities.AsNoTracking(), filter)
            .CountAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IQueryable<Activity> Apply(IQueryable<Activity> source, ActivityListFilter filter)
    {
        if (filter.EntityType is { Length: > 0 } entityType)
        {
            source = source.Where(a => a.EntityType == entityType);
        }
        if (filter.EntityId is { } entityId)
        {
            source = source.Where(a => a.EntityId == entityId);
        }
        if (filter.AssignedToUserId is { } assignee)
        {
            source = source.Where(a => a.AssignedToUserId == assignee);
        }
        if (filter.Status is { } status)
        {
            ActivityStatus mapped = status switch
            {
                ActivityStatusFilter.OpenOrOverdue => ActivityStatus.Open,
                ActivityStatusFilter.Done => ActivityStatus.Done,
                ActivityStatusFilter.Cancelled => ActivityStatus.Cancelled,
                _ => ActivityStatus.Open,
            };
            source = source.Where(a => a.Status == mapped);
        }
        if (filter.DueAtFrom is { } from)
        {
            source = source.Where(a => a.DueAt >= from);
        }
        if (filter.DueAtTo is { } to)
        {
            source = source.Where(a => a.DueAt < to);
        }
        return source;
    }
}
