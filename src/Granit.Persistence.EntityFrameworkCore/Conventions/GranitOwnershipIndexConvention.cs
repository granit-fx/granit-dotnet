using Granit.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Granit.Persistence.EntityFrameworkCore.Conventions;

/// <summary>
/// Native form of the ownership-index pass (#3158): <see cref="IOwnable"/> entities get a
/// composite index on <c>(TenantId, OwnerId)</c> when also <see cref="IMultiTenant"/>, or on
/// <c>(OwnerId)</c> alone — named <c>ix_{table}_{tenant_owner|owner}</c>. De-duplicates
/// against an existing index over the exact same property set.
/// </summary>
internal sealed class GranitOwnershipIndexConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (IConventionEntityType entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            ApplyOwnershipIndex(entityType);
        }
    }

    private static void ApplyOwnershipIndex(IConventionEntityType entityType)
    {
        if (!typeof(IOwnable).IsAssignableFrom(entityType.ClrType))
        {
            return;
        }

        string? tableName = entityType.GetTableName();
        if (tableName is null)
        {
            return; // Keyless / TPH-derived / view-mapped — no own table to index.
        }

        bool isMultiTenant = typeof(IMultiTenant).IsAssignableFrom(entityType.ClrType);
        string[] propertyNames = isMultiTenant
            ? [nameof(IMultiTenant.TenantId), nameof(IOwnable.OwnerId)]
            : [nameof(IOwnable.OwnerId)];

        if (!TryResolveProperties(entityType, propertyNames, out List<IConventionProperty> properties)
            || IsAlreadyIndexed(entityType, propertyNames))
        {
            return;
        }

        string suffix = isMultiTenant ? "tenant_owner" : "owner";
        entityType.Builder.HasIndex(properties)
            ?.Metadata.SetDatabaseName($"ix_{tableName}_{suffix}");
    }

    private static bool TryResolveProperties(
        IConventionEntityType entityType,
        string[] propertyNames,
        out List<IConventionProperty> properties)
    {
        properties = [];
        foreach (string name in propertyNames)
        {
            IConventionProperty? property = entityType.FindProperty(name);
            if (property is null)
            {
                properties.Clear();
                return false;
            }

            properties.Add(property);
        }

        return true;
    }

    private static bool IsAlreadyIndexed(IConventionEntityType entityType, string[] propertyNames) =>
        entityType.GetIndexes().Any(idx =>
            idx.Properties.Count == propertyNames.Length
            && idx.Properties.Select(p => p.Name).SequenceEqual(propertyNames));
}
