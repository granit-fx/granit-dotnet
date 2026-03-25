namespace Granit.QueryEngine;

/// <summary>
/// Grouped result for query endpoints with single-level grouping.
/// </summary>
/// <typeparam name="T">The item type (entity or DTO).</typeparam>
/// <param name="Groups">The groups with their counts, aggregates, and optional items.</param>
/// <param name="TotalCount">The total number of matching items across all groups.</param>
public sealed record GroupedResult<T>(
    IReadOnlyList<GroupEntry<T>> Groups,
    int TotalCount);

/// <summary>
/// A single group entry within a <see cref="GroupedResult{T}"/>.
/// </summary>
/// <typeparam name="T">The item type (entity or DTO).</typeparam>
public sealed record GroupEntry<T>
{
    /// <summary>
    /// The property name used for grouping (e.g. <c>"Status"</c>).
    /// </summary>
    public required string Field { get; init; }

    /// <summary>
    /// The group key value (e.g. <c>"Active"</c>, <c>42</c>, <c>null</c>).
    /// </summary>
    public required object? Value { get; init; }

    /// <summary>
    /// Display label for the group (e.g. localized enum value).
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Number of items in this group.
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// Computed aggregates for this group, keyed by alias
    /// (e.g. <c>{ "totalAmount": 12345.67 }</c>).
    /// <c>null</c> when no aggregates are defined.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Aggregates { get; init; }

    /// <summary>
    /// Items within this group. Only populated when the client requests drill-down.
    /// <c>null</c> for summary-only responses.
    /// </summary>
    public IReadOnlyList<T>? Items { get; init; }
}
