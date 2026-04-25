using System.Collections.Concurrent;

namespace Granit.Persistence.EntityFrameworkCore.Metadata;

/// <summary>
/// Default implementation of <see cref="IMetadataMappingRegistry"/>.
/// Populated at DI registration time; thread-safe for concurrent reads.
/// </summary>
internal sealed class MetadataMappingRegistry : IMetadataMappingRegistry
{
    private static readonly HashSet<string> EmptyNames = [];
    private static readonly IReadOnlyList<MetadataMapping> EmptyMappings = [];

    private readonly ConcurrentDictionary<Type, (HashSet<string> Names, IReadOnlyList<MetadataMapping> Mappings)>
        _registrations = new();

    /// <inheritdoc/>
    public HashSet<string> GetMappedPropertyNames(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return _registrations.TryGetValue(entityType, out (HashSet<string> Names, IReadOnlyList<MetadataMapping> Mappings) entry)
            ? entry.Names
            : EmptyNames;
    }

    /// <inheritdoc/>
    public IReadOnlyList<MetadataMapping> GetMappings(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return _registrations.TryGetValue(entityType, out (HashSet<string> Names, IReadOnlyList<MetadataMapping> Mappings) entry)
            ? entry.Mappings
            : EmptyMappings;
    }

    /// <summary>
    /// Registers property mappings for an entity type. Called at DI registration time.
    /// </summary>
    /// <param name="entityType">The CLR type of the entity.</param>
    /// <param name="mappings">The property mappings to register.</param>
    internal void Register(Type entityType, IReadOnlyList<MetadataMapping> mappings)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(mappings);

        if (mappings.Count == 0)
        {
            return;
        }

        HashSet<string> names = new(mappings.Select(m => m.Name), StringComparer.Ordinal);

        _registrations.AddOrUpdate(
            entityType,
            _ => (names, mappings),
            (_, existing) =>
            {
                // Merge with existing registrations (multiple modules may register for the same type)
                foreach (string name in names)
                {
                    existing.Names.Add(name);
                }

                List<MetadataMapping> merged = [.. existing.Mappings, .. mappings];
                return (existing.Names, merged);
            });
    }
}
