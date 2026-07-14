namespace Granit.DataExchange.Import.Pipeline;

/// <summary>
/// Registry of typed import pipeline descriptors, one per registered
/// <see cref="ImportDefinition{TEntity}"/>.
/// </summary>
/// <remarks>
/// Built once at startup from the <see cref="IImportEntityBinding"/> singletons registered by
/// <c>AddImportDefinition&lt;TEntity, TDefinition&gt;()</c>. Lookups are
/// <see cref="StringComparer.Ordinal"/> — definition names are culture-invariant wire identifiers.
/// </remarks>
public interface IImportPipelineRegistry
{
    /// <summary>
    /// Gets all descriptors in stable (ordinal by name) order.
    /// </summary>
    IReadOnlyList<IImportPipelineDescriptor> GetAll();

    /// <summary>
    /// Finds a descriptor by definition name (ordinal comparison).
    /// </summary>
    /// <param name="definitionName">The definition name (e.g. <c>"Acme.PatientImport"</c>).</param>
    /// <returns>The descriptor, or <c>null</c> when no definition with that name is registered.</returns>
    IImportPipelineDescriptor? Find(string definitionName);
}
