using Granit.DataExchange.Export.Pipeline;
using Granit.QueryEngine;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.DataExchange.Export.Internal;

/// <summary>
/// Default <see cref="IExportPipelineDescriptor"/> implementation carrying the closed entity
/// type. Instantiated by the registry either through the entity-binding visitor (explicit
/// definitions, zero reflection) or through a single generic instantiation at registry
/// initialization (auto-generated definitions).
/// </summary>
internal sealed class ExportPipelineDescriptor<TEntity>(IExportDefinitionDescriptor definition) : IExportPipelineDescriptor
    where TEntity : class
{
    /// <inheritdoc/>
    public string DefinitionName => definition.Name;

    /// <inheritdoc/>
    public Type EntityType => typeof(TEntity);

    /// <inheritdoc/>
    public IExportDefinitionDescriptor Definition => definition;

    /// <inheritdoc/>
    public IExportPipeline Create(IServiceProvider scopedProvider)
    {
        ArgumentNullException.ThrowIfNull(scopedProvider);

        return new ExportPipeline<TEntity>(
            definition,
            scopedProvider.GetRequiredService<IExportDataSource<TEntity>>(),
            definition.QueryDefinitionName is null
                ? null
                : scopedProvider.GetRequiredService<IQueryEngine<TEntity>>(),
            scopedProvider.GetRequiredService<IExtraExportFieldProvider>(),
            scopedProvider.GetRequiredService<IExportExtraValueResolver>());
    }
}
