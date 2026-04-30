namespace Granit.Entities.Relations;

/// <summary>
/// Closed enum of display modes a relation can pick on the source entity's
/// detail view (per ADR-048). Renderer treatment is opaque to the framework —
/// the enum is the contract; clients map each value to a concrete UI slot.
/// </summary>
public enum RelationDisplay
{
    /// <summary>Dedicated tab in the detail view (full-screen list of related rows).</summary>
    Tab,

    /// <summary>Compact button in the detail header — opens a drilldown when clicked.</summary>
    SmartButton,

    /// <summary>Always-visible side panel in the detail right rail.</summary>
    Sidebar,

    /// <summary>Inline chip strip embedded in a section (limited count, click-to-expand).</summary>
    InlineChips,
}
