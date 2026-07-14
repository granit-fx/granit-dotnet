namespace Granit.DataExchange.Export;

/// <summary>
/// Resolves export definitions by merging explicit <see cref="IExportDefinitionDescriptor"/>
/// registrations with auto-generated <c>ReflectionExportDefinition</c> fallbacks.
/// </summary>
/// <remarks>
/// <para>
/// Explicit definitions (registered via
/// <c>ServiceCollectionExtensions.AddExportDefinition{TEntity,TDefinition}</c>)
/// always take precedence over auto-generated ones.
/// </para>
/// <para>
/// Auto-generated definitions are built lazily from entity types discovered by
/// <see cref="IAutoExportDefinitionSource"/> implementations.
/// </para>
/// </remarks>
public interface IExportDefinitionProvider
{
    /// <summary>
    /// Finds an export definition by name (ordinal, case-sensitive).
    /// Returns <c>null</c> if no definition exists with the given name.
    /// </summary>
    /// <param name="definitionName">The definition name to look up.</param>
    IExportDefinitionDescriptor? FindByName(string definitionName);

    /// <summary>
    /// Returns all available export definitions (explicit + auto-generated).
    /// Explicit definitions take precedence when an entity type has both.
    /// </summary>
    IReadOnlyList<IExportDefinitionDescriptor> GetAll();
}
