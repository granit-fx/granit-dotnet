using Granit.Entities.Endpoints.Dtos;
using Granit.Entities.Layouts;

namespace Granit.Entities.EntityFrameworkCore.Internal;

/// <summary>
/// Non-generic façade over a per-entity <see cref="CalendarRangeRunner{TEntity}"/>.
/// Lets the dispatcher (<see cref="EntityFrameworkCoreCalendarRangeService"/>) build a
/// dictionary keyed on entity name and pick the right closed-generic runner without
/// any reflection at request time. Mirrors the
/// <c>IMetricRunner</c>/<c>IQueryAggregateRunner</c> pattern in
/// <c>Granit.Analytics.EntityFrameworkCore</c>.
/// </summary>
internal interface ICalendarRangeRunner
{
    /// <summary>CLR type name of the entity this runner handles (e.g. <c>"Party"</c>).</summary>
    string EntityName { get; }

    /// <summary>Runs the calendar query and projects the rows into <see cref="CalendarItemResponse"/>.</summary>
    Task<IReadOnlyList<CalendarItemResponse>> ExecuteAsync(
        CalendarLayoutDescriptor layout,
        Endpoints.CalendarRange range,
        string? displayProperty,
        CancellationToken cancellationToken);
}
