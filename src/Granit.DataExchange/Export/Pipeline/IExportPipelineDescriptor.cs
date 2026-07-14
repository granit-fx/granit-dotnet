namespace Granit.DataExchange.Export.Pipeline;

/// <summary>
/// Registration-time metadata for a typed export pipeline, plus a factory that binds the
/// pipeline to a service scope at execution time.
/// </summary>
/// <remarks>
/// Descriptors are built once by the <see cref="IExportPipelineRegistry"/> — either from an
/// explicit <c>IExportEntityBinding</c> (zero reflection) or from an auto-discovered entity type
/// (a single generic instantiation at registry initialization). The scoped services a pipeline
/// needs (data source, query engine, extra-value resolver) are resolved inside
/// <see cref="Create(IServiceProvider)"/> because the registry itself is a singleton.
/// </remarks>
public interface IExportPipelineDescriptor
{
    /// <summary>
    /// The export definition name (ordinal, case-sensitive registry key).
    /// </summary>
    string DefinitionName { get; }

    /// <summary>
    /// The source entity CLR type.
    /// </summary>
    Type EntityType { get; }

    /// <summary>
    /// The export definition descriptor backing this pipeline.
    /// </summary>
    IExportDefinitionDescriptor Definition { get; }

    /// <summary>
    /// Creates a pipeline instance bound to the given scope.
    /// </summary>
    /// <param name="scopedProvider">
    /// The scoped service provider used to resolve <c>IExportDataSource&lt;TEntity&gt;</c>,
    /// <c>IQueryEngine&lt;TEntity&gt;</c> (when the definition references a query definition),
    /// and the extra-property services.
    /// </param>
    /// <returns>A pipeline ready to execute one export.</returns>
    IExportPipeline Create(IServiceProvider scopedProvider);
}
