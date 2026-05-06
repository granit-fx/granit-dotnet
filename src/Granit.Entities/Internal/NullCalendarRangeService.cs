using Granit.Entities.Layouts;

namespace Granit.Entities.Internal;

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
