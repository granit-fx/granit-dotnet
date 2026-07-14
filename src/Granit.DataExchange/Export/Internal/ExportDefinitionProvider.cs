using Granit.DataExchange.Export.Pipeline;

namespace Granit.DataExchange.Export.Internal;

/// <summary>
/// Default <see cref="IExportDefinitionProvider"/> implementation, backed by the
/// <see cref="IExportPipelineRegistry"/> so metadata endpoints and the export pipeline
/// resolve definitions from the same single source of truth.
/// </summary>
/// <remarks>
/// Resolution semantics come from the registry: explicit definitions win over auto-generated
/// ones, and name lookups are ordinal (case-sensitive).
/// </remarks>
internal sealed class ExportDefinitionProvider(IExportPipelineRegistry registry) : IExportDefinitionProvider
{
    /// <inheritdoc/>
    public IExportDefinitionDescriptor? FindByName(string definitionName) =>
        registry.Find(definitionName)?.Definition;

    /// <inheritdoc/>
    public IReadOnlyList<IExportDefinitionDescriptor> GetAll() =>
        [.. registry.GetAll().Select(d => d.Definition)];
}
