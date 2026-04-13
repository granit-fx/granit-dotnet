using Granit.QueryEngine;
using Granit.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Scheduling.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="ScheduledAction"/>.
/// </summary>
internal sealed class EfScheduledActionQueryableSource(
    IDbContextFactory<SchedulingDbContext> contextFactory) : IQueryableSource<ScheduledAction>
{
    private readonly SchedulingDbContext _context = contextFactory.CreateDbContext();

    public IQueryable<ScheduledAction> GetQueryable() =>
        _context.ScheduledActions.AsNoTracking();
}
