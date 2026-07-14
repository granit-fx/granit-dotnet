namespace Granit.DataExchange.Export.Pipeline;

/// <summary>
/// Singleton registry of all typed export pipelines, composed from explicit
/// <c>IExportEntityBinding</c> registrations and auto-discovered entity types
/// (<see cref="IAutoExportDefinitionSource"/>).
/// </summary>
/// <remarks>
/// <para>
/// Explicit definitions always win: an auto-generated definition is only materialized for an
/// entity type that has no explicit <see cref="ExportDefinition{TEntity}"/> registered.
/// </para>
/// <para>
/// Lookups are <b>ordinal and case-sensitive</b>. A miss returns <see langword="null"/>;
/// callers surface the registered names in their error messages.
/// </para>
/// </remarks>
public interface IExportPipelineRegistry
{
    /// <summary>
    /// Returns all pipeline descriptors (explicit first, then auto-generated).
    /// </summary>
    IReadOnlyList<IExportPipelineDescriptor> GetAll();

    /// <summary>
    /// Finds a pipeline descriptor by definition name (ordinal comparison).
    /// Returns <see langword="null"/> when no definition exists with the given name.
    /// </summary>
    /// <param name="definitionName">The definition name to look up.</param>
    IExportPipelineDescriptor? Find(string definitionName);
}
