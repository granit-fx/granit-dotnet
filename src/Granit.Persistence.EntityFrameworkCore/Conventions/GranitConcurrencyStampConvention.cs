using Granit.Domain;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Granit.Persistence.EntityFrameworkCore.Conventions;

/// <summary>
/// Native form of the concurrency-stamp pass (#3158): <see cref="IConcurrencyAware"/>
/// entities get <c>ConcurrencyStamp</c> as a VARCHAR(36) concurrency token.
/// </summary>
internal sealed class GranitConcurrencyStampConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (IConventionEntityType entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            if (!typeof(IConcurrencyAware).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            IConventionProperty? stamp = entityType.FindProperty(nameof(IConcurrencyAware.ConcurrencyStamp));
            if (stamp is null)
            {
                continue;
            }

            // Parity with the legacy pass, which uses the explicit Fluent API and therefore
            // ALWAYS pins VARCHAR(36) — even over an entity configuration's own value. The
            // mutable surface writes with explicit configuration source; a convention-source
            // set would be refused. Revisit the stomp itself in the Phase 3 cleanup.
            var mutableStamp = (IMutableProperty)stamp;
            mutableStamp.SetMaxLength(36);
            mutableStamp.IsConcurrencyToken = true;
        }
    }
}
