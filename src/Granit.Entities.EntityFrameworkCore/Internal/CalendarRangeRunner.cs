using System.Linq.Expressions;
using Granit.Entities.Layouts;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Entities.EntityFrameworkCore.Internal;

/// <summary>
/// Closed-generic implementation that runs the calendar query for one entity type.
/// Resolved at composition time (one runner per registered <c>EntityDefinition</c>),
/// so the request-time dispatch in <see cref="EntityFrameworkCoreCalendarRangeService"/>
/// is a dictionary lookup with zero reflection.
/// </summary>
/// <typeparam name="TEntity">Entity type the calendar layout was declared on.</typeparam>
internal sealed class CalendarRangeRunner<TEntity>(IQueryableSource<TEntity> source) : ICalendarRangeRunner
    where TEntity : class
{
    public string EntityName { get; } = typeof(TEntity).Name;

    public async Task<IReadOnlyList<CalendarItemResponse>> ExecuteAsync(
        CalendarLayoutDescriptor layout,
        CalendarRange range,
        string? displayProperty,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(layout);

        Expression<Func<TEntity, bool>> overlap = CalendarRangeFilterBuilder.BuildOverlapFilter<TEntity>(
            layout,
            new CalendarFilterRange(range.From, range.To));

        Expression<Func<TEntity, CalendarItemResponse>> projection =
            CalendarItemProjectionBuilder.Build<TEntity>(layout, displayProperty);

        return await source.GetQueryable()
            .Where(overlap)
            .Select(projection)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
