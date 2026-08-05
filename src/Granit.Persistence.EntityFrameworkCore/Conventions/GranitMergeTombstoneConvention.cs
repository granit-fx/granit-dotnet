using Granit.Domain;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Granit.Persistence.EntityFrameworkCore.Conventions;

/// <summary>
/// Native form of the merge-tombstone pass (#3158): <see cref="IHasMergeTombstone"/>
/// entities get <c>MergedIntoId</c>/<c>MergedAt</c> columns and an index on
/// <c>MergedIntoId</c> ("who merged into X" lookups + admin tombstone listings).
/// </summary>
internal sealed class GranitMergeTombstoneConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (IConventionEntityType entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            if (!typeof(IHasMergeTombstone).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            IConventionProperty? mergedInto =
                entityType.Builder.Property(typeof(Guid?), nameof(IHasMergeTombstone.MergedIntoId))?.Metadata;
            entityType.Builder.Property(typeof(DateTimeOffset?), "MergedAt");

            if (mergedInto is not null && entityType.FindIndex([mergedInto]) is null)
            {
                entityType.Builder.HasIndex([mergedInto]);
            }
        }
    }
}
