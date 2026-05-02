using Granit.Entities.Endpoints;
using Granit.Entities.Endpoints.Dtos;
using Granit.Entities.Layouts;

namespace Granit.Entities.EntityFrameworkCore.Internal;

/// <summary>
/// Default <see cref="ICalendarRangeService"/> when the EF Core executor module is
/// loaded. Looks up the right closed-generic <see cref="ICalendarRangeRunner"/> by
/// entity name and delegates the actual query — the per-entity runners are wired at
/// composition time by
/// <see cref="Extensions.EntitiesEntityFrameworkCoreServiceCollectionExtensions.AddGranitEntitiesEntityFrameworkCore"/>,
/// so this dispatcher does not allocate per call.
/// </summary>
/// <remarks>
/// Returns an empty list when no runner matches the entity (the corresponding
/// <c>EntityDefinition</c> never declared a calendar layout, or the host did not
/// register an <c>IQueryableSource&lt;T&gt;</c> for it). The endpoint has already
/// validated permissions and selected the layout — silent fallback keeps the wire
/// shape predictable for the React shell, matching <c>NullCalendarRangeService</c>.
/// </remarks>
internal sealed class EntityFrameworkCoreCalendarRangeService(
    IEnumerable<ICalendarRangeRunner> runners) : ICalendarRangeService
{
    private readonly Dictionary<string, ICalendarRangeRunner> _byEntity =
        runners.ToDictionary(r => r.EntityName, StringComparer.Ordinal);

    public Task<IReadOnlyList<CalendarItemResponse>> GetItemsAsync(
        EntityDefinitionDescriptor entity,
        CalendarLayoutDescriptor layout,
        CalendarRange range,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(layout);

        return _byEntity.TryGetValue(entity.EntityType.Name, out ICalendarRangeRunner? runner)
            ? runner.ExecuteAsync(layout, range, entity.DisplayProperty, cancellationToken)
            : Task.FromResult<IReadOnlyList<CalendarItemResponse>>([]);
    }
}
