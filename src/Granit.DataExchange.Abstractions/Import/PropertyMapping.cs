namespace Granit.DataExchange.Import;

/// <summary>
/// Immutable metadata about a single importable property, built from <see cref="PropertyMappingBuilder"/>.
/// </summary>
public sealed class PropertyMapping
{
    /// <summary>Property path on the entity (e.g. <c>"Email"</c>).</summary>
    public required string PropertyPath { get; init; }

    /// <summary>CLR type name of the property (e.g. <c>"String"</c>).</summary>
    public required string ClrTypeName { get; init; }

    /// <summary>User-facing display name, or <c>null</c>.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Property description, or <c>null</c>.</summary>
    public string? Description { get; init; }

    /// <summary>Alternative column names for matching.</summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    /// <summary>Whether this property is required for import.</summary>
    public bool IsRequired { get; init; }

    /// <summary>Expected format for type conversion, or <c>null</c>.</summary>
    public string? Format { get; init; }

    /// <summary>
    /// Converts this mapping to a <see cref="ImportFieldMetadata"/> for the mapping suggestion pipeline.
    /// </summary>
    internal ImportFieldMetadata ToFieldMetadata() =>
        new(PropertyPath, ClrTypeName, DisplayName, Description, IsRequired);
}
