using Granit.DataExchange.Import.Pipeline;

namespace Granit.DataExchange.Import.Internal;

/// <summary>
/// Default <see cref="IImportPipelineRegistry"/> built once from the
/// <see cref="IImportEntityBinding"/> singletons registered by
/// <c>AddImportDefinition&lt;TEntity, TDefinition&gt;()</c>. Mirrors
/// <c>QueryDefinitionRegistry</c>: a stable-ordered snapshot plus an ordinal by-name index.
/// </summary>
internal sealed class ImportPipelineRegistry : IImportPipelineRegistry
{
    private readonly IReadOnlyList<IImportPipelineDescriptor> _ordered;
    private readonly Dictionary<string, IImportPipelineDescriptor> _byName;

    public ImportPipelineRegistry(IEnumerable<IImportEntityBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        DescriptorFactory factory = new();
        List<IImportPipelineDescriptor> descriptors = [];
        Dictionary<string, IImportPipelineDescriptor> byName = new(StringComparer.Ordinal);

        foreach (IImportEntityBinding binding in bindings)
        {
            IImportPipelineDescriptor descriptor = binding.Accept(factory);
            if (!byName.TryAdd(descriptor.DefinitionName, descriptor))
            {
                throw new InvalidOperationException(
                    $"Duplicate import definition name '{descriptor.DefinitionName}' " +
                    $"(entity types '{byName[descriptor.DefinitionName].EntityType.Name}' and '{descriptor.EntityType.Name}'). " +
                    "Import definition names must be globally unique — rename one of the definitions.");
            }

            descriptors.Add(descriptor);
        }

        // Stable ordinal ordering keeps test fixtures deterministic and matches
        // the wire identifier's culture-invariant nature.
        _ordered = [.. descriptors.OrderBy(d => d.DefinitionName, StringComparer.Ordinal)];
        _byName = byName;
    }

    /// <inheritdoc/>
    public IReadOnlyList<IImportPipelineDescriptor> GetAll() => _ordered;

    /// <inheritdoc/>
    public IImportPipelineDescriptor? Find(string definitionName)
    {
        ArgumentException.ThrowIfNullOrEmpty(definitionName);
        return _byName.TryGetValue(definitionName, out IImportPipelineDescriptor? descriptor) ? descriptor : null;
    }

    /// <summary>
    /// Visits each binding exactly once to close the descriptor over the entity type.
    /// </summary>
    private sealed class DescriptorFactory : IImportEntityVisitor<IImportPipelineDescriptor>
    {
        public IImportPipelineDescriptor Visit<TEntity>(ImportDefinition<TEntity> definition) where TEntity : class =>
            new ImportPipelineDescriptor<TEntity>(definition);
    }
}
