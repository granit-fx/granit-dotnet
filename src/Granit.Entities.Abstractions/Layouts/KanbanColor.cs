namespace Granit.Entities.Layouts;

/// <summary>
/// Closed catalog of column colours for the kanban layout. Each value maps to a
/// design-token in the renderer's theme — the wire never carries a hex code, so
/// tenant theming and dark-mode swaps stay declarative.
/// </summary>
public enum KanbanColor
{
    /// <summary>Theme-neutral grey — for archived / terminal states.</summary>
    Neutral,

    /// <summary>Cool grey — for draft / pending states.</summary>
    Gray,

    /// <summary>Blue — for in-progress / active states.</summary>
    Blue,

    /// <summary>Green — for completed / success states.</summary>
    Green,

    /// <summary>Orange — for waiting / attention states.</summary>
    Orange,

    /// <summary>Red — for blocked / error states.</summary>
    Red,

    /// <summary>Yellow — for warning / soft-attention states.</summary>
    Yellow,

    /// <summary>Purple — for special / categorical highlights.</summary>
    Purple,

    /// <summary>Cyan — for informational / secondary highlights.</summary>
    Cyan,
}
