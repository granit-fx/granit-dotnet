namespace Granit.Persistence.EntityFrameworkCore;

/// <summary>
/// Annotation keys set by Granit persistence helpers on EF Core metadata
/// (properties, entity types, models). Consumed by provider-specific
/// conventions to apply provider-tuned configuration without coupling
/// the base library to a particular database engine.
/// </summary>
public static class GranitPersistenceAnnotationNames
{
    /// <summary>
    /// Marker set on a property by
    /// <see cref="Extensions.JsonPropertyBuilderExtensions.HasJsonConversion{T}"/>.
    /// Indicates the property is persisted as a JSON-serialized string.
    /// Provider-specific conventions (e.g. PostgreSQL <c>jsonb</c>) can scan
    /// for this annotation to upgrade the column type.
    /// </summary>
    public const string JsonSerialized = "Granit:JsonSerialized";
}
