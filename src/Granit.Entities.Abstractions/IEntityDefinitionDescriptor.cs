namespace Granit.Entities;

/// <summary>
/// Non-generic accessor for an <see cref="EntityDefinition{TEntity}"/> registered
/// in DI. Used by the <c>Granit.Entities</c> runtime registry to enumerate every
/// entity's UI surface without knowing the concrete entity type.
/// </summary>
public interface IEntityDefinitionDescriptor
{
    /// <summary>Wire identifier (e.g. <c>"Granit.Parties.Party"</c>).</summary>
    string Name { get; }

    /// <summary>The entity's CLR type.</summary>
    Type EntityType { get; }

    /// <summary>The fully-built immutable descriptor for the entity's UI surface.</summary>
    EntityDefinitionDescriptor Descriptor { get; }
}
