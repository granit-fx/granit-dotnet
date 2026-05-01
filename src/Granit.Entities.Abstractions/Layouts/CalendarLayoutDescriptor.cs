namespace Granit.Entities.Layouts;

/// <summary>
/// Calendar layout — time-axis presentation (month / week / day grid) keyed on
/// a date or datetime property. The renderer (<c>EntityCalendar</c>) reads
/// items via the range-query endpoint and positions each one between
/// <see cref="StartPropertyName"/> and the optional <see cref="EndPropertyName"/>.
/// </summary>
/// <remarks>
/// Per ADR-042 §1, only <see cref="StartPropertyName"/> is mandatory; events
/// without an end render as point-in-time markers. <see cref="ColorByPropertyName"/>
/// — when set — drives per-event colour grouping client-side; the property
/// must appear in the matching <c>QueryDefinition</c> column whitelist (enforced
/// by an architecture test).
/// </remarks>
public sealed record CalendarLayoutDescriptor : EntityListLayoutDescriptor
{
    /// <summary>Property carrying the event start (required).</summary>
    public required string StartPropertyName { get; init; }

    /// <summary>Property carrying the event end. <see langword="null"/> renders point-in-time markers.</summary>
    public string? EndPropertyName { get; init; }

    /// <summary>Property used as the event's headline on the tile.</summary>
    public string? TitlePropertyName { get; init; }

    /// <summary>Property used to bucket events into colour groups (typically an enum or status).</summary>
    public string? ColorByPropertyName { get; init; }
}
