namespace Granit.Persistence.ExtraProperties;

/// <summary>
/// Describes a single extra property mapping to a real SQL column (Shadow Property).
/// </summary>
/// <param name="Name">The property name (used as column name and ExtraProperties key).</param>
/// <param name="ClrType">The CLR type of the property.</param>
/// <param name="MaxLength">Maximum string length (<see langword="null"/> for non-string types).</param>
/// <param name="IsRequired">Whether the column is NOT NULL.</param>
/// <param name="IsFilterable">Whether the property should be exposed as a filterable column in Querying.</param>
/// <param name="IsSortable">Whether the property should be exposed as a sortable column in Querying.</param>
public sealed record ExtraPropertyMapping(
    string Name,
    Type ClrType,
    int? MaxLength,
    bool IsRequired,
    bool IsFilterable,
    bool IsSortable);
