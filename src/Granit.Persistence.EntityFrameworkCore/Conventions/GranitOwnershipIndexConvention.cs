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
            if (!typeof(IOwnable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            string? tableName = entityType.GetTableName();
            if (tableName is null)
            {
                continue; // Keyless / TPH-derived / view-mapped — no own table to index.
            }

            bool isMultiTenant = typeof(IMultiTenant).IsAssignableFrom(entityType.ClrType);
            string[] propertyNames = isMultiTenant
                ? [nameof(IMultiTenant.TenantId), nameof(IOwnable.OwnerId)]
                : [nameof(IOwnable.OwnerId)];

            List<IConventionProperty> properties = [];
            foreach (string name in propertyNames)
            {
                IConventionProperty? property = entityType.FindProperty(name);
                if (property is null)
                {
                    properties.Clear();
                    break;
                }

                properties.Add(property);
            }

            if (properties.Count == 0)
            {
                continue;
            }

            bool alreadyIndexed = entityType.GetIndexes().Any(idx =>
                idx.Properties.Count == propertyNames.Length
                && idx.Properties.Select(p => p.Name).SequenceEqual(propertyNames));

            if (alreadyIndexed)
            {
                continue;
            }

            string suffix = isMultiTenant ? "tenant_owner" : "owner";
            entityType.Builder.HasIndex(properties)
                ?.Metadata.SetDatabaseName($"ix_{tableName}_{suffix}");
        }
    }
}
