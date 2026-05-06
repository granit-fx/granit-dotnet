using Granit.Entities.Layouts;

namespace Granit.Entities;

/// <summary>
/// Resolves calendar items for one entity within a time window. The default
/// in-package implementation returns an empty list — hosts wire up a real
/// EF Core executor (story #1689) by replacing this registration.
/// </summary>
/// <remarks>
/// Lives in the runtime base module alongside <see cref="CalendarItemResponse"/>
/// and the null implementation so a non-HTTP host (e.g. a projector) can
/// resolve calendar items without pulling <c>Granit.Entities.Endpoints</c>.
/// Same placement rule as <c>IRelationAggregateService</c>.
/// </remarks>
public interface ICalendarRangeService
{
    /// <summary>Returns the calendar items in <paramref name="range"/> for one entity layout.</summary>
    /// <param name="entity">The entity descriptor (carries the EntityType + name).</param>
    /// <param name="layout">The selected calendar layout (drives the StartField / EndField / Title / ColorBy projection).</param>
    /// <param name="range">Inclusive time window — already validated as <c>To &gt;= From</c> and within the maximum span.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<CalendarItemResponse>> GetItemsAsync(
        EntityDefinitionDescriptor entity,
        CalendarLayoutDescriptor layout,
        CalendarRange range,
        CancellationToken cancellationToken);
}

/// <summary>Inclusive time window for the calendar query.</summary>
public readonly record struct CalendarRange(DateTimeOffset From, DateTimeOffset To);
