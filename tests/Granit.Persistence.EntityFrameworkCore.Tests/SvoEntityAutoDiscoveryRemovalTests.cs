// =============================================================================
// Repro — SVO entity auto-discovery removal must detach orphan FKs
// =============================================================================
// When a domain entity has a property of an SVO type (e.g. `BlobReference`,
// which is `SingleValueObject<string>`), EF Core's auto-discovery scanner
// treats the reference type as a navigation and registers the SVO as an
// entity type, then auto-creates a foreign key from the owner.
//
// `RemoveSingleValueObjectEntityTypes` in `ApplyGranitConventions` exists to
// purge those phantom SVO entities so the matching converter can map the
// property as a scalar column. But until this fix it called `RemoveEntityType`
// directly and EF rejects that whenever a referencing FK still exists:
//
//   System.InvalidOperationException :
//     The entity type 'BlobReference' cannot be removed because it is being
//     referenced by foreign key {'AvatarTempId'} on 'Party'.
//
// Reported by granit-business during the GranitDbContext migration on Parties
// (Party.AvatarTempId : BlobReference) without an explicit
// modelBuilder.Ignore<BlobReference>().
//
// The fix detaches any FK whose principal entity is an SVO before removing
// the SVO entity itself. The underlying scalar column survives —
// ApplySingleValueObjectConverters wraps it with a converter to round-trip
// through the primitive form.
// =============================================================================

using Granit.Domain.ValueObjects;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class SvoEntityAutoDiscoveryRemovalTests
{
    [Fact]
    public void ApplyGranitConventions_EntityWithSvoProperty_RemovesSvoAndDetachesOrphanForeignKey()
    {
        // Arrange — model the granit-business "Party" scenario in miniature:
        // an entity exposes a property whose CLR type is an SVO. EF Core's
        // convention scanner discovers it as an entity AND wires a FK before
        // OnModelCreating runs.
        using SvoOwnerDbContext context = new(new DbContextOptionsBuilder<SvoOwnerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        // Act — model build triggers ApplyGranitConventions on first access.
        IReadOnlyList<string> entityNames = [.. context.Model.GetEntityTypes()
            .Select(et => et.ClrType.Name)];

        // Assert — the SVO must NOT appear as an entity (it is a value object).
        entityNames.ShouldContain(nameof(SvoOwnerEntity), "the owner entity must remain");
        entityNames.ShouldNotContain(nameof(BlobReference),
            "the SVO type must NOT survive as an entity type");

        // The scalar column survives via ApplySingleValueObjectConverters.
        Microsoft.EntityFrameworkCore.Metadata.IEntityType owner = context.Model
            .FindEntityType(typeof(SvoOwnerEntity))!;
        owner.FindProperty(nameof(SvoOwnerEntity.AvatarTempId)).ShouldNotBeNull(
            "the SVO column must survive as a scalar property on the owner");
    }

    public sealed class SvoOwnerEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        // BlobReference is a SingleValueObject<string> in Granit.Domain.
        // No Ignore<BlobReference>() configuration — relies on the framework
        // convention to do the right thing.
        public BlobReference? AvatarTempId { get; set; }
    }

    private sealed class SvoOwnerDbContext(DbContextOptions<SvoOwnerDbContext> options)
        : DbContext(options)
    {
        public DbSet<SvoOwnerEntity> Owners => Set<SvoOwnerEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.ApplyGranitConventions();
    }
}
