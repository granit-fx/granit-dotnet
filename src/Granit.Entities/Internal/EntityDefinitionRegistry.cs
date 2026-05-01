using Granit.Entities.Actions;
using Granit.Entities.Relations;
using Microsoft.Extensions.Logging;

namespace Granit.Entities.Internal;

/// <summary>
/// Default <see cref="IEntityDefinitionRegistry"/> built from the DI-injected
/// enumerable of <see cref="IEntityDefinitionDescriptor"/>. Detects duplicate
/// names + duplicate entity types at construction (fails fast). Folds
/// cross-module relation contributions into the matching descriptors via
/// <see cref="EntityRelationMerger"/>, then folds action contributions via
/// <see cref="EntityActionMerger"/>, before indexing.
/// </summary>
internal sealed class EntityDefinitionRegistry : IEntityDefinitionRegistry
{
    private readonly Dictionary<string, IEntityDefinitionDescriptor> _byName;
    private readonly Dictionary<Type, IEntityDefinitionDescriptor> _byEntityType;

    public EntityDefinitionRegistry(
        IEnumerable<IEntityDefinitionDescriptor> definitions,
        IEnumerable<IEntityRelationContributor> relationContributors,
        IEnumerable<IEntityActionContributor> actionContributors,
        ILogger<EntityDefinitionRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(relationContributors);
        ArgumentNullException.ThrowIfNull(actionContributors);
        ArgumentNullException.ThrowIfNull(logger);

        IReadOnlyList<IEntityDefinitionDescriptor> withRelations =
            EntityRelationMerger.Merge(definitions, relationContributors, logger);
        IReadOnlyList<IEntityDefinitionDescriptor> merged =
            EntityActionMerger.Merge(withRelations, actionContributors, logger);

        _byName = new Dictionary<string, IEntityDefinitionDescriptor>(StringComparer.Ordinal);
        _byEntityType = [];

        foreach (IEntityDefinitionDescriptor definition in merged)
        {
            if (!_byName.TryAdd(definition.Name, definition))
            {
                throw new InvalidOperationException(
                    $"Duplicate EntityDefinition name '{definition.Name}'. Names must be unique across all loaded modules.");
            }

            if (!_byEntityType.TryAdd(definition.EntityType, definition))
            {
                throw new InvalidOperationException(
                    $"Duplicate EntityDefinition for CLR type '{definition.EntityType.FullName}'. At most one EntityDefinition may target a given entity type.");
            }
        }

        All = [.. _byName.Values.OrderBy(d => d.Name, StringComparer.Ordinal)];
    }

    public IReadOnlyList<IEntityDefinitionDescriptor> All { get; }

    public IEntityDefinitionDescriptor? GetByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _byName.TryGetValue(name, out IEntityDefinitionDescriptor? definition) ? definition : null;
    }

    public IEntityDefinitionDescriptor? GetByEntityType(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return _byEntityType.TryGetValue(entityType, out IEntityDefinitionDescriptor? definition) ? definition : null;
    }
}
