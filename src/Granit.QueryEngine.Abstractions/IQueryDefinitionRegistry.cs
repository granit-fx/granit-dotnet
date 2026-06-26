namespace Granit.QueryEngine;

/// <summary>
/// Read-only registry of every <see cref="QueryDefinition{TEntity}"/> registered across
/// all loaded modules. Surfaces the query catalogue to the <c>GET /catalog</c> endpoint
/// so a dashboard editor can offer a dropdown of queries instead of a free-text
/// <c>queryName</c>.
/// </summary>
public interface IQueryDefinitionRegistry
{
    /// <summary>
    /// Returns every registered descriptor, ordered by <see cref="IQueryDefinitionDescriptor.Name"/>
    /// (ordinal). Stable across processes — useful for tests and snapshot fixtures.
    /// </summary>
    IReadOnlyList<IQueryDefinitionDescriptor> GetAll();

    /// <summary>
    /// Looks up a descriptor by its wire identifier. Returns <c>null</c> when the name is
    /// not registered (callers translate to a 404 at the HTTP layer).
    /// </summary>
    IQueryDefinitionDescriptor? Find(string name);
}
