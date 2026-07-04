namespace Granit.Entities.Layouts;

/// <summary>
/// Default render state of a kanban column. The framework declares the value;
/// per-user overlays (saved <c>EntityView</c>) may override it.
/// </summary>
public enum KanbanColumnState
{
    /// <summary>Expanded — header + cards visible.</summary>
    Open,

    /// <summary>Collapsed — header + count only.</summary>
    Collapsed,

    /// <summary>Hidden — column is not rendered (toggleable from the column manager).</summary>
    Hidden,
}
