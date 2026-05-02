namespace Granit.Entities.Layouts;

/// <summary>
/// Closed enumeration of the list-view layouts an entity may declare via the
/// manifest (per ADR-040). The renderer's <c>EntityListViewSwitcher</c> picks
/// the active layout from this list — single-layout entities skip the switcher
/// entirely.
/// </summary>
public enum EntityListLayoutKind
{
    /// <summary>Default tabular layout — driven by the entity's <c>QueryDefinition</c> columns.</summary>
    List = 0,

    /// <summary>Card-board layout grouped by a discrete property (typically an enum or lookup).</summary>
    Kanban = 1,

    /// <summary>Time-axis layout (month / week / day grid) keyed on a date or datetime property.</summary>
    Calendar = 2,

    /// <summary>Image-card grid layout keyed on a <c>BlobReference</c> property.</summary>
    Gallery = 3,
}
