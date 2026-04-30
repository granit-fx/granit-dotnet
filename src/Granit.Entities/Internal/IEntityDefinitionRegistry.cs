namespace Granit.Entities.Internal;

/// <summary>
/// Read-side registry that exposes every registered
/// <see cref="IEntityDefinitionDescriptor"/> by name and by entity CLR type.
/// Singleton — built once at boot from the DI snapshot.
/// </summary>
public interface IEntityDefinitionRegistry
{
    /// <summary>All registered entity definitions, sorted by <see cref="IEntityDefinitionDescriptor.Name"/>.</summary>
    IReadOnlyList<IEntityDefinitionDescriptor> All { get; }

    /// <summary>Get the definition by its wire identifier (e.g. <c>"Granit.Parties.Party"</c>), or <see langword="null"/> when unknown.</summary>
    IEntityDefinitionDescriptor? GetByName(string name);

    /// <summary>Get the definition by its CLR entity type, or <see langword="null"/> when unknown.</summary>
    IEntityDefinitionDescriptor? GetByEntityType(Type entityType);
}
