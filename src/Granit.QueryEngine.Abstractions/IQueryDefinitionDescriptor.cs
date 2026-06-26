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

    /// <summary>
    /// The localization resource used to resolve this query's display label. The localizer is
    /// queried with the key <c>"Query:{Name}"</c>, falling back to <see cref="Name"/> when the
    /// key is absent. <c>null</c> means no localization is attempted. This is the same resource
    /// that resolves the query's column labels.
    /// </summary>
    Type? LocalizationResourceType { get; }
}
