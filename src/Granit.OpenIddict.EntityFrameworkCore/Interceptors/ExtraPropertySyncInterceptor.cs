using System.Text.Json;
using Granit.OpenIddict.Entities;
using Granit.OpenIddict.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace Granit.OpenIddict.EntityFrameworkCore.Interceptors;

/// <summary>
/// EF Core interceptor that synchronizes <see cref="GranitUser.CustomAttributesJson"/>
/// with dynamically mapped Shadow Properties, preventing data duplication.
/// </summary>
/// <remarks>
/// <para>
/// When a property is mapped as a SQL column via <see cref="GranitUserExtensionOptions"/>,
/// it is <b>excluded</b> from <c>CustomAttributesJson</c> at save time. The SQL column
/// is the source of truth for mapped properties (indexable, queryable).
/// </para>
/// <para>
/// On save: reads <c>ExtraProperties</c>, writes mapped values to Shadow Properties,
/// and removes them from <c>CustomAttributesJson</c>.
/// </para>
/// </remarks>
internal sealed class ExtraPropertySyncInterceptor(
    IOptions<GranitUserExtensionOptions> extensionOptions) : SaveChangesInterceptor
{
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

        GranitUserExtensionOptions options = extensionOptions.Value;
        if (options.Mappings.Count == 0)
        {
            return ValueTask.FromResult(result);
        }

        var mappedNames = options.Mappings.Select(m => m.Name).ToHashSet(StringComparer.Ordinal);

        foreach (EntityEntry<GranitUser> entry in eventData.Context.ChangeTracker
            .Entries<GranitUser>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            SyncProperties(entry, mappedNames);
        }

        return ValueTask.FromResult(result);
    }

    private static void SyncProperties(EntityEntry<GranitUser> entry, HashSet<string> mappedNames)
    {
        GranitUser user = entry.Entity;

        // Parse current JSON
        Dictionary<string, string> jsonProps = string.IsNullOrWhiteSpace(user.CustomAttributesJson)
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, string>>(user.CustomAttributesJson) ?? [];

        bool jsonModified = false;

        foreach (string mappedName in mappedNames)
        {
            // If the property exists in JSON, move its value to the Shadow Property
            if (jsonProps.TryGetValue(mappedName, out string? value))
            {
                entry.Property(mappedName).CurrentValue = value;
                jsonProps.Remove(mappedName);
                jsonModified = true;
            }
        }

        // Update JSON without the mapped properties
        if (jsonModified)
        {
            user.CustomAttributesJson = jsonProps.Count > 0
                ? JsonSerializer.Serialize(jsonProps)
                : null;
        }
    }
}
