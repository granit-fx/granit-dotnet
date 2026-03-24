using Granit.ReferenceData.Domain;

namespace Granit.ReferenceData.Options;

/// <summary>
/// Options for configuring a dynamic reference data type with custom properties
/// and optional hierarchical support.
/// </summary>
/// <remarks>
/// <para>
/// Used with the simplified <c>AddReferenceData&lt;TDbContext&gt;()</c> registration API.
/// Properties declared via <see cref="MapProperty{T}"/> are stored as real SQL columns
/// (EF Core Shadow Properties) that are indexable and filterable via Granit.Querying.
/// </para>
/// <para>
/// Additional properties can be stored in the JSON bag via
/// <see cref="Granit.Domain.ExtraPropertyExtensions.SetExtraProperty"/> without
/// needing to declare them here.
/// </para>
/// </remarks>
public sealed class ReferenceDataExtensionOptions
{
    /// <summary>
    /// Gets or sets the database table name for this reference data type.
    /// </summary>
    public string TableName { get; set; } = "ref_data";

    /// <summary>
    /// Gets or sets whether this reference data type supports parent-child hierarchy
    /// via <see cref="ReferenceDataEntity.ParentCode"/>.
    /// </summary>
    public bool IsHierarchical { get; set; }

    /// <summary>
    /// Gets the list of dynamic property mappings to real SQL columns.
    /// </summary>
    internal List<ReferenceDataPropertyMapping> PropertyMappings { get; } = [];

    /// <summary>
    /// Sets the database table name for this reference data type.
    /// </summary>
    /// <param name="tableName">The table name (e.g., <c>"ref_countries"</c>).</param>
    /// <returns>This options instance for chaining.</returns>
    public ReferenceDataExtensionOptions Table(string tableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        TableName = tableName;
        return this;
    }

    /// <summary>
    /// Enables hierarchical support (parent-child via <c>ParentCode</c>).
    /// </summary>
    /// <returns>This options instance for chaining.</returns>
    public ReferenceDataExtensionOptions Hierarchical()
    {
        IsHierarchical = true;
        return this;
    }

    /// <summary>
    /// Maps a property as a real SQL column on the reference data table.
    /// </summary>
    /// <typeparam name="T">The CLR type of the property.</typeparam>
    /// <param name="name">The property name (used as column name and ExtraProperties key).</param>
    /// <param name="maxLength">Maximum string length (only for <see cref="string"/> properties).</param>
    /// <param name="isRequired">Whether the column is NOT NULL. Default: <see langword="false"/>.</param>
    /// <param name="isFilterable">Whether the property is filterable in Querying. Default: <see langword="false"/>.</param>
    /// <param name="isSortable">Whether the property is sortable in Querying. Default: <see langword="false"/>.</param>
    /// <returns>This options instance for chaining.</returns>
    public ReferenceDataExtensionOptions MapProperty<T>(
        string name,
        int? maxLength = null,
        bool isRequired = false,
        bool isFilterable = false,
        bool isSortable = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        PropertyMappings.Add(new ReferenceDataPropertyMapping(name, typeof(T), maxLength, isRequired, isFilterable, isSortable));
        return this;
    }
}

/// <summary>
/// Describes a single dynamic property mapping for a reference data type.
/// </summary>
/// <param name="Name">The property name (column name + ExtraProperties key).</param>
/// <param name="ClrType">The CLR type of the property.</param>
/// <param name="MaxLength">Maximum string length (<see langword="null"/> for non-string types).</param>
/// <param name="IsRequired">Whether the column is NOT NULL.</param>
/// <param name="IsFilterable">Whether the property is filterable in Querying.</param>
/// <param name="IsSortable">Whether the property is sortable in Querying.</param>
public sealed record ReferenceDataPropertyMapping(
    string Name,
    Type ClrType,
    int? MaxLength,
    bool IsRequired,
    bool IsFilterable,
    bool IsSortable);
