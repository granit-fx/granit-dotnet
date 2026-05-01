namespace Granit.Entities.Layouts;

/// <summary>
/// Closed catalog of column colours for the kanban layout. Each value maps to a
/// design-token in the renderer's theme — the wire never carries a hex code, so
/// tenant theming and dark-mode swaps stay declarative.
/// </summary>
public enum KanbanColor
{
    /// <summary>Theme-neutral grey — for archived / terminal states.</summary>
    Neutral = 0,

    /// <summary>Cool grey — for draft / pending states.</summary>
    Gray = 1,

    /// <summary>Blue — for in-progress / active states.</summary>
    Blue = 2,

    /// <summary>Green — for completed / success states.</summary>
    Green = 3,

    /// <summary>Orange — for waiting / attention states.</summary>
    Orange = 4,

    /// <summary>Red — for blocked / error states.</summary>
    Red = 5,

    /// <summary>Yellow — for warning / soft-attention states.</summary>
    Yellow = 6,

    /// <summary>Purple — for special / categorical highlights.</summary>
    Purple = 7,

    /// <summary>Cyan — for informational / secondary highlights.</summary>
    Cyan = 8,
}
