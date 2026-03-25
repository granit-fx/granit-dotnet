namespace Granit.QueryEngine.Meta;

/// <summary>
/// Complete metadata for a query endpoint, returned by <c>GET /meta</c>.
/// Enables frontend auto-configuration of list views (columns, filters, sorting, pagination).
/// </summary>
public sealed record QueryMetadata
{
    /// <summary>Column definitions for the data table.</summary>
    public required IReadOnlyList<ColumnDefinition> Columns { get; init; }

    /// <summary>Filterable fields with their available operators.</summary>
    public required IReadOnlyList<FilterableField> FilterableFields { get; init; }

    /// <summary>Sortable field names.</summary>
    public required IReadOnlyList<SortableField> SortableFields { get; init; }

    /// <summary>Preset filter groups with mutually exclusive options.</summary>
    public required IReadOnlyList<FilterGroupMeta> PresetFilterGroups { get; init; }

    /// <summary>Independent toggleable quick filters.</summary>
    public required IReadOnlyList<QuickFilterMeta> QuickFilters { get; init; }

    /// <summary>Date filter shortcuts.</summary>
    public required IReadOnlyList<DateFilterMeta> DateFilters { get; init; }

    /// <summary>Fields allowed for group-by operations.</summary>
    public required IReadOnlyList<GroupByField> GroupByFields { get; init; }

    /// <summary>Pagination configuration.</summary>
    public required PaginationMeta Pagination { get; init; }

    /// <summary>Default sort specification, or <c>null</c>.</summary>
    public string? DefaultSort { get; init; }
}
