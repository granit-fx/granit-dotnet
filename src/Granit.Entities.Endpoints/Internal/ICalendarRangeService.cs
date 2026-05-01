using Granit.Entities.Endpoints.Dtos;
using Granit.Entities.Layouts;

namespace Granit.Entities.Endpoints.Internal;

/// <summary>
/// Resolves calendar items for one entity within a time window. The default
/// in-package implementation returns an empty list — hosts wire up a real
/// EF Core executor (story #1689) by replacing this registration.
/// </summary>
/// <remarks>
/// <para>
/// The interface lives in <c>.Endpoints</c> rather than the abstractions
/// package so the wire DTO (<see cref="CalendarItemResponse"/>) stays adjacent
/// to its only producer / consumer. Same placement rule as
/// <c>IRelationAggregateService</c>.
/// </para>
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

/// <summary>
/// No-op implementation registered by default. Returns an empty result —
/// suitable for hosts that have not yet wired the EF Core executor or for
/// modules that expose a calendar layout purely for the manifest.
/// </summary>
internal sealed class NullCalendarRangeService : ICalendarRangeService
{
    public Task<IReadOnlyList<CalendarItemResponse>> GetItemsAsync(
        EntityDefinitionDescriptor entity,
        CalendarLayoutDescriptor layout,
        CalendarRange range,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CalendarItemResponse>>([]);
}
