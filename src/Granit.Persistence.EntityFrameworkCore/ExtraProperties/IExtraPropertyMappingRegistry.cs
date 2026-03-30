namespace Granit.Persistence.EntityFrameworkCore.ExtraProperties;

/// <summary>
/// Singleton registry that aggregates all <see cref="ExtraPropertyMappingOptions{TEntity}"/>
/// by entity type. Used by <see cref="ExtraPropertySyncInterceptor"/> to resolve
/// mapped property names at save time.
/// </summary>
public interface IExtraPropertyMappingRegistry
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
    IReadOnlyList<ExtraPropertyMapping> GetMappings(Type entityType);
}
