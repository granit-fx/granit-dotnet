using Granit.QueryEngine.Filtering;

namespace Granit.QueryEngine.Meta;

/// <summary>
/// Filterable field metadata for frontend auto-configuration.
/// </summary>
/// <param name="Name">Property name.</param>
/// <param name="Type">CLR type name.</param>
/// <param name="Operators">Available filter operators for this field.</param>
/// <param name="EnumValues">
/// For enum-typed fields, the set of valid value names (e.g. <c>["Active", "Inactive"]</c>).
/// <see langword="null"/> for non-enum fields. Lets the frontend render a dropdown of
/// suggestions instead of a free-text input when entering the filter value.
/// </param>
public sealed record FilterableField(
    string Name,
    string Type,
    IReadOnlyList<FilterOperator> Operators,
    IReadOnlyList<string>? EnumValues = null);
