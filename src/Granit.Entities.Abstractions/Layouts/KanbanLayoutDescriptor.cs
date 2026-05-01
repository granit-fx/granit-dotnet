namespace Granit.Entities.Layouts;

/// <summary>
/// Kanban layout — board grouped by a discrete property (typically an enum or
/// lookup-table value). Combines the group-by axis, the per-tile card schema,
/// and per-value column metadata.
/// </summary>
public sealed record KanbanLayoutDescriptor : EntityListLayoutDescriptor
{
    /// <summary>Property on the entity used to bucket rows into columns.</summary>
    public required string GroupByPropertyName { get; init; }

    /// <summary>CLR type of the group-by property — drives wire serialisation of column values.</summary>
    public required Type GroupByClrType { get; init; }

    /// <summary>Card-content schema rendered inside each tile.</summary>
    public required KanbanCardDescriptor Card { get; init; }

    /// <summary>Per-value column metadata, in declaration order.</summary>
    public required IReadOnlyList<KanbanColumnDescriptor> Columns { get; init; }
}
