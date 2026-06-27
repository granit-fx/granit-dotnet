namespace Granit.QueryEngine;

/// <summary>
/// Marker interface for query definition discovery via DI.
/// </summary>
/// <seealso cref="QueryDefinition{TEntity}"/>
public interface IQueryDefinitionDescriptor
{
    /// <summary>
    /// Unique name identifying this query definition (e.g. <c>"Acme.Patients"</c>).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The entity type this definition applies to.
    /// </summary>
    Type EntityType { get; }
}
