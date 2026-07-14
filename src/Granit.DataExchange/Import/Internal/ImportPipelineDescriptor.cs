using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Mapping.Internal;
using Granit.DataExchange.Import.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.DataExchange.Import.Internal;

/// <summary>
/// Typed <see cref="IImportPipelineDescriptor"/> closing over <typeparamref name="TEntity"/>.
/// Holds the definition plus a lazily-compiled default data mapper (built once, thread-safe).
/// </summary>
/// <typeparam name="TEntity">The target entity type.</typeparam>
internal sealed class ImportPipelineDescriptor<TEntity>(ImportDefinition<TEntity> definition)
    : IImportPipelineDescriptor
    where TEntity : class
{
    private readonly Lazy<CompiledDataMapper<TEntity>> _compiledMapper = new(
        () => new CompiledDataMapper<TEntity>(definition),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <inheritdoc/>
    public string DefinitionName => definition.Name;

    /// <inheritdoc/>
    public Type EntityType => typeof(TEntity);

    /// <inheritdoc/>
    public IImportDefinitionDescriptor Definition => definition;

    /// <inheritdoc/>
    public IImportPipeline Create(IServiceProvider scopedProvider)
    {
        ArgumentNullException.ThrowIfNull(scopedProvider);
        return new ImportPipeline<TEntity>(definition, _compiledMapper, scopedProvider);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ImportColumnMapping>> SuggestMappingsAsync(
        IServiceProvider scopedProvider,
        IReadOnlyList<string> headers,
        IReadOnlyList<string[]>? previewRows,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scopedProvider);
        return scopedProvider.GetRequiredService<IMappingSuggestionService>()
            .SuggestMappingsAsync<TEntity>(headers, previewRows, cancellationToken);
    }
}
