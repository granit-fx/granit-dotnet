using Granit.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Scheduling.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IScheduledActionQueryableProvider"/>.
/// Exposes <see cref="ScheduledAction"/> as an <see cref="IQueryable{T}"/> source
/// for <c>MapGranitQuery</c> pagination, filtering, and sorting.
/// </summary>
internal sealed class EfScheduledActionQueryableProvider(
    IDbContextFactory<SchedulingDbContext> contextFactory)
    : IScheduledActionQueryableProvider
{
    private readonly SchedulingDbContext _context = contextFactory.CreateDbContext();

    /// <inheritdoc />
    public IQueryable<ScheduledAction> GetScheduledActions() =>
        _context.ScheduledActions.AsNoTracking();
}
