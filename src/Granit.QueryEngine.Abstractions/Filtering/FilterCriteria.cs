namespace Granit.QueryEngine.Filtering;

/// <summary>
/// A single filter criterion parsed from the query string.
/// </summary>
/// <param name="Field">The property name to filter on (e.g. <c>"name"</c>).</param>
/// <param name="Operator">The filter operator (e.g. <see cref="FilterOperator.Contains"/>).</param>
/// <param name="Value">The filter value as a string (e.g. <c>"Alice"</c>).</param>
public sealed record FilterCriteria(
    string Field,
    FilterOperator Operator,
    string Value);
