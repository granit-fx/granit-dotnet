namespace Granit.Entities.Layouts;

/// <summary>
/// Fluent sub-builder for one kanban column — sets the column's color and the
/// default render state (Open / Collapsed / Hidden).
/// </summary>
public sealed class KanbanColumnBuilder
{
    private KanbanColor? _color;
    private KanbanColumnState _state = KanbanColumnState.Open;

    internal KanbanColumnBuilder() { }

    /// <summary>Sets the column's colour from the closed catalog.</summary>
    public KanbanColumnBuilder Color(KanbanColor color)
    {
        _color = color;
        return this;
    }

    /// <summary>Marks the column as collapsed by default (header + count only).</summary>
    public KanbanColumnBuilder Collapsed()
    {
        _state = KanbanColumnState.Collapsed;
        return this;
    }

    /// <summary>
    /// Marks the column as hidden by default — the user can opt back in via the
    /// column manager. Use for archived / wind-down states the renderer should
    /// not surface unless explicitly requested.
    /// </summary>
    public KanbanColumnBuilder Hidden()
    {
        _state = KanbanColumnState.Hidden;
        return this;
    }

    internal KanbanColumnDescriptor Build(string value) =>
        new()
        {
            Value = value,
            Color = _color,
            DefaultState = _state,
        };
}
