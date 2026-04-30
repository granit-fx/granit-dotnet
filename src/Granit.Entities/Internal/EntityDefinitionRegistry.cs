namespace Granit.Entities.Internal;

/// <summary>
/// Default <see cref="IEntityDefinitionRegistry"/> built from the DI-injected
/// enumerable of <see cref="IEntityDefinitionDescriptor"/>. Detects duplicate
/// names + duplicate entity types at construction (fails fast).
/// </summary>
internal sealed class EntityDefinitionRegistry : IEntityDefinitionRegistry
{
    private readonly Dictionary<string, IEntityDefinitionDescriptor> _byName;
    private readonly Dictionary<Type, IEntityDefinitionDescriptor> _byEntityType;

    public EntityDefinitionRegistry(IEnumerable<IEntityDefinitionDescriptor> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        _byName = new Dictionary<string, IEntityDefinitionDescriptor>(StringComparer.Ordinal);
        _byEntityType = [];

        foreach (IEntityDefinitionDescriptor definition in definitions)
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
