using System.Collections.Concurrent;
using System.Text.Json;
using Granit.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Granit.Persistence.EntityFrameworkCore.Metadata;

/// <summary>
/// EF Core interceptor that synchronizes <see cref="IHasMetadata.MetadataJson"/>
/// with dynamically mapped Shadow Properties, preventing data duplication.
/// </summary>
/// <remarks>
/// <para>
/// A single instance handles <b>all</b> entity types implementing <see cref="IHasMetadata"/>.
/// Property mappings are resolved once per entity type from <see cref="IMetadataMappingRegistry"/>
/// and cached in a <see cref="ConcurrentDictionary{TKey,TValue}"/> for the app's lifetime.
/// </para>
/// <para>
/// On save: for each modified <see cref="IHasMetadata"/> entity, reads the JSON bag,
/// writes mapped values to their Shadow Properties, and removes them from the JSON to avoid
/// storing the same data twice.
/// </para>
/// </remarks>
internal sealed class MetadataSyncInterceptor(
    IMetadataMappingRegistry registry) : SaveChangesInterceptor
{
    private readonly ConcurrentDictionary<Type, HashSet<string>> _cache = new();

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
        {
            return ValueTask.FromResult(result);
        }

        foreach (EntityEntry entry in eventData.Context.ChangeTracker
            .Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified
                     && e.Entity is IHasMetadata))
        {
            Type entityType = entry.Entity.GetType();
            HashSet<string> mappedNames = _cache.GetOrAdd(entityType, registry.GetMappedPropertyNames);

            if (mappedNames.Count > 0)
            {
                SyncProperties(entry, (IHasMetadata)entry.Entity, mappedNames);
            }
        }

        return ValueTask.FromResult(result);
    }

    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is null)
        {
            return result;
        }

        foreach (EntityEntry entry in eventData.Context.ChangeTracker
            .Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified
                     && e.Entity is IHasMetadata))
        {
            Type entityType = entry.Entity.GetType();
            HashSet<string> mappedNames = _cache.GetOrAdd(entityType, registry.GetMappedPropertyNames);

            if (mappedNames.Count > 0)
            {
                SyncProperties(entry, (IHasMetadata)entry.Entity, mappedNames);
            }
        }

        return result;
    }

    private static void SyncProperties(
        EntityEntry entry,
        IHasMetadata entity,
        HashSet<string> mappedNames)
    {
        Dictionary<string, string> jsonProps = string.IsNullOrWhiteSpace(entity.MetadataJson)
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, string>>(entity.MetadataJson) ?? [];

        bool jsonModified = false;

        foreach (string mappedName in mappedNames)
        {
            if (jsonProps.TryGetValue(mappedName, out string? value))
            {
                entry.Property(mappedName).CurrentValue = value;
                jsonProps.Remove(mappedName);
                jsonModified = true;
            }
        }

        if (jsonModified)
        {
            entity.MetadataJson = jsonProps.Count > 0
                ? JsonSerializer.Serialize(jsonProps)
                : null;
        }
    }
}
