namespace Granit.Entities.Endpoints.Dtos;

/// <summary>
/// One event positioned on the calendar's time axis. The renderer
/// (<c>EntityCalendar</c>) reads this shape to lay out tiles.
/// </summary>
/// <param name="Id">Stable identifier of the underlying entity row — drives the detail-page link.</param>
/// <param name="Start">Event start, projected from the calendar's <c>StartField</c>.</param>
/// <param name="End">Event end, projected from the calendar's <c>EndField</c>. <see langword="null"/> renders a point-in-time marker.</param>
/// <param name="Title">Event headline, projected from the calendar's <c>TitleField</c> when set, otherwise the entity's <c>DisplayProperty</c>.</param>
/// <param name="Color">Stable colour bucket for this event, projected from the calendar's <c>ColorBy</c> when set. <see langword="null"/> falls back to the theme default.</param>
public sealed record CalendarItemResponse(
    Guid Id,
    DateTimeOffset Start,
    DateTimeOffset? End,
    string Title,
    string? Color);
