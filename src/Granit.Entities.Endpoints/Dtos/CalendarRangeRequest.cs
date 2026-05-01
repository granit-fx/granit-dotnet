namespace Granit.Entities.Endpoints.Dtos;

/// <summary>
/// Wire shape for the range-query parameters of <c>GET /api/entities/{name}/calendar</c>.
/// Bound from the query string by ASP.NET Core's model binder.
/// </summary>
/// <param name="From">Inclusive start of the requested window.</param>
/// <param name="To">Inclusive end of the requested window.</param>
/// <param name="Calendar">
/// Optional name of the target <c>CalendarLayoutDescriptor</c> when an entity
/// declares more than one (reserved for future kinds — today the framework
/// rejects duplicate layout kinds, so leaving this <see langword="null"/>
/// always picks the entity's single calendar).
/// </param>
public sealed record CalendarRangeRequest(
    DateTimeOffset From,
    DateTimeOffset To,
    string? Calendar);
