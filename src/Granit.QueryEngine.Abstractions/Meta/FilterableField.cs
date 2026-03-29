using Granit.QueryEngine.Filtering;

namespace Granit.QueryEngine.Meta;

/// <summary>
/// Filterable field metadata for frontend auto-configuration.
/// </summary>
/// <param name="Name">Property name.</param>
/// <param name="Type">CLR type name.</param>
/// <param name="Operators">Available filter operators for this field.</param>
public sealed record FilterableField(
    string Name,
    string Type,
    IReadOnlyList<FilterOperator> Operators);
