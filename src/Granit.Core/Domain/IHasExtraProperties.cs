namespace Granit.Core.Domain;

/// <summary>
/// Marker interface for entities that support a JSON-serialized property bag
/// for application-level extensibility.
/// </summary>
/// <remarks>
/// <para>
/// Properties stored in <see cref="ExtraPropertiesJson"/> are flexible key-value pairs
/// that do not require schema changes. For properties that need SQL indexes or query
/// filtering, use <c>ExtraPropertyMappingOptions&lt;T&gt;.MapProperty()</c> to promote
/// them to real SQL columns (Shadow Properties).
/// </para>
/// <para>
/// Use the extension methods in <see cref="ExtraPropertyExtensions"/> for typed
/// access to individual properties.
/// </para>
/// </remarks>
public interface IHasExtraProperties
{
    /// <summary>
    /// Gets or sets the JSON-serialized extra properties string.
    /// </summary>
    /// <remarks>
    /// Stored as a <c>jsonb</c> column (PostgreSQL) or <c>nvarchar(max)</c> (SQL Server).
    /// The value is a JSON object with string keys and string values.
    /// </remarks>
    string? ExtraPropertiesJson { get; set; }
}
