namespace Granit.Entities.Layouts;

/// <summary>
/// Per-value column declaration for a kanban layout — pairs a discrete value
/// of the <c>GroupBy</c> property with optional rendering metadata (colour,
/// default visibility state). The framework declares server-known defaults;
/// per-user overlays (saved <c>EntityView</c>) may override them.
/// </summary>
public sealed record KanbanColumnDescriptor
{
    /// <summary>
    /// The discrete value of the <c>GroupBy</c> property this column matches.
    /// Stored as the value's wire form (e.g. enum member name, string literal).
    /// </summary>
    public required string Value { get; init; }

    /// <summary>Optional column colour from the closed catalog.</summary>
    public KanbanColor? Color { get; init; }

    /// <summary>Default render state — Open, Collapsed, or Hidden.</summary>
    public KanbanColumnState DefaultState { get; init; } = KanbanColumnState.Open;
}
