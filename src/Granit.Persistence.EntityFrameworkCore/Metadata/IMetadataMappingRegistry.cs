namespace Granit.Persistence.EntityFrameworkCore.Metadata;

/// <summary>
/// Singleton registry that aggregates all <see cref="MetadataMappingOptions{TEntity}"/>
/// by entity type. Used by <see cref="MetadataSyncInterceptor"/> to resolve
/// mapped property names at save time.
/// </summary>
public interface IMetadataMappingRegistry
{
    /// <summary>
    /// Gets the set of mapped property names for the given entity type.
    /// Returns an empty set if no mappings are registered.
    /// </summary>
    /// <param name="entityType">The CLR type of the entity.</param>
    /// <returns>A set of mapped property names.</returns>
    HashSet<string> GetMappedPropertyNames(Type entityType);

    /// <summary>
    /// Gets all property mappings for the given entity type.
    /// Returns an empty list if no mappings are registered.
    /// </summary>
    /// <param name="entityType">The CLR type of the entity.</param>
    /// <returns>The list of property mappings.</returns>
    IReadOnlyList<MetadataMapping> GetMappings(Type entityType);
}
