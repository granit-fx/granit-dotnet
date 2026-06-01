// =============================================================================
// Repro — multi-field ValueObject auto-discovery removal (SEO shard crash)
// =============================================================================
// `ApplyGranitConventions` un-mapped SingleValueObject<T> entity types but not
// multi-field value objects (types deriving from `Granit.Domain.ValueObject`
// with more than one field). When an entity exposes such a value object as a
// property — directly or nested — EF Core's convention scanner registers it
// (and every nested value object) as an entity type and then fails model
// validation while binding their constructors:
//
//   System.InvalidOperationException :
//     No suitable constructor was found for the type 'ImageDimensions'.
//     Cannot bind 'width', 'height' in 'ImageDimensions(int width, int height)'
//
// Reported by the SEO integration shard: SeoMetadata.OpenGraph (OpenGraph →
// OgImage → ImageDimensions) crashed the model build before any test touched
// Postgres.
//
// The fix un-maps every auto-discovered ValueObject. Multi-field value objects
// are re-attached as a JSON-serialized column (marked JsonSerialized so the
// Postgres provider upgrades it to jsonb) — the whole graph, including nested
// value objects, lives inside the blob. A value object mapped explicitly as an
// EF complex type is never an entity type, so the convention leaves it alone.
// =============================================================================

using Granit.Domain.ValueObjects;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class ValueObjectEntityAutoDiscoveryRemovalTests
{
    [Fact]
    public void ApplyGranitConventions_EntityWithNestedValueObject_RemovesValueObjectsAndSerializesAsJson()
    {
        // Arrange — model the SEO scenario: an entity exposes a multi-field value
        // object that nests further value objects (OpenGraph → OgImage → ImageDimensions).
        using SeoOwnerDbContext context = new(new DbContextOptionsBuilder<SeoOwnerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        // Act — model build triggers ApplyGranitConventions on first access. Without the
        // fix this throws "No suitable constructor was found for the type 'ImageDimensions'".
        IReadOnlyList<string> entityNames = [.. context.Model.GetEntityTypes()
            .Select(et => et.ClrType.Name)];

        // Assert — only the real entity survives; every value object in the graph is gone.
        entityNames.ShouldContain(nameof(SeoMetadataEntity), "the owner entity must remain");
        entityNames.ShouldNotContain(nameof(TestOpenGraph), "the top-level value object must not be an entity");
        entityNames.ShouldNotContain(nameof(TestOgImage), "the nested value object must not be an entity");
        entityNames.ShouldNotContain(nameof(ImageDimensions), "the leaf value object must not be an entity");

        // The property survives as a JSON-serialized column wrapping the whole graph.
        Microsoft.EntityFrameworkCore.Metadata.IEntityType owner = context.Model
            .FindEntityType(typeof(SeoMetadataEntity))!;
        Microsoft.EntityFrameworkCore.Metadata.IProperty property =
            owner.FindProperty(nameof(SeoMetadataEntity.OpenGraph)).ShouldNotBeNull(
                "the value object must survive as a scalar (JSON) property");

        property.GetValueConverter().ShouldNotBeNull("a JSON value converter must be wired");
        property.FindAnnotation(GranitPersistenceAnnotationNames.JsonSerialized)?.Value
            .ShouldBe(true, "the property must be flagged so the Postgres provider upgrades it to jsonb");
    }

    [Fact]
    public void ApplyGranitConventions_EntityWithExplicitOwnsOneOnValueObject_FailsFastWithGuidance()
    {
        // Arrange — explicit OwnsOne(...) on a ValueObject subtype is unsupported. The
        // convention must fail fast at model build with a message directing the author to one
        // of the two supported paths (convention auto-JSON / scalar, or ComplexProperty for
        // typed flat columns). Silent-override would discard the per-property configuration
        // (column names, HasMaxLength, etc.) and surface only as a runtime schema drift or
        // STJ deserialization error.
        using ExplicitOwnsOneDbContext context = new(new DbContextOptionsBuilder<ExplicitOwnsOneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        // Act + Assert — touching context.Model triggers OnModelCreating.
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() => _ = context.Model);

        // The message must name the offending pattern, point at the typed-columns alternative,
        // cite the two ADRs, and locate the offending entity + property by name.
        ex.Message.ShouldContain("OwnsOne");
        ex.Message.ShouldContain("ComplexProperty");
        ex.Message.ShouldContain("ADR-017");
        ex.Message.ShouldContain("ADR-058");
        ex.Message.ShouldContain(nameof(OwnsOneOwnerEntity));
        ex.Message.ShouldContain(nameof(OwnsOneOwnerEntity.Location));
    }

    [Fact]
    public void ApplyGranitConventions_ValueObjectMappedAsComplexProperty_IsLeftUntouched()
    {
        // Arrange — opt out of the jsonb default by declaring the value object as an EF
        // complex type. It maps to flat columns (Latitude, Longitude) and is not an entity
        // type, so the convention must not touch it (no removal, no JSON converter).
        using ComplexOwnerDbContext context = new(new DbContextOptionsBuilder<ComplexOwnerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        // Act
        Microsoft.EntityFrameworkCore.Metadata.IEntityType owner = context.Model
            .FindEntityType(typeof(ComplexOwnerEntity))!;

        // Assert — GeoCoordinate stays a complex type with flat members, not a JSON blob.
        owner.GetComplexProperties()
            .ShouldContain(cp => cp.Name == nameof(ComplexOwnerEntity.Location),
                "the value object must remain mapped as a complex property");

        Microsoft.EntityFrameworkCore.Metadata.IComplexProperty complex =
            owner.FindComplexProperty(nameof(ComplexOwnerEntity.Location))!;
        complex.ComplexType.GetProperties().Select(p => p.Name)
            .ShouldBe([nameof(GeoCoordinate.Latitude), nameof(GeoCoordinate.Longitude)], ignoreOrder: true);
    }

    // --- Test value objects modelling the SEO chain (multi-field, nested). -----------------
    // ImageDimensions is the real framework value object reused as the leaf.

    public sealed class TestOgImage(string url, ImageDimensions dimensions) : Granit.Domain.ValueObject
    {
        public string Url { get; } = url;
        public ImageDimensions Dimensions { get; } = dimensions;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Url;
            yield return Dimensions;
        }
    }

    public sealed class TestOpenGraph(string title, TestOgImage image) : Granit.Domain.ValueObject
    {
        public string Title { get; } = title;
        public TestOgImage Image { get; } = image;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Title;
            yield return Image;
        }
    }

    public sealed class SeoMetadataEntity
    {
        public Guid Id { get; set; }

        // No explicit configuration — relies on the framework convention.
        public TestOpenGraph? OpenGraph { get; set; }
    }

    // A multi-field value object with settable members — a clean EF complex-type candidate
    // (EF complex-type constructor binding is more limited than entity binding).
    public sealed class GeoCoordinate : Granit.Domain.ValueObject
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Latitude;
            yield return Longitude;
        }
    }

    public sealed class ComplexOwnerEntity
    {
        public Guid Id { get; set; }
        public GeoCoordinate Location { get; set; } = new();
    }

    public sealed class OwnsOneOwnerEntity
    {
        public Guid Id { get; set; }
        public GeoCoordinate Location { get; set; } = new();
    }

    private sealed class SeoOwnerDbContext(DbContextOptions<SeoOwnerDbContext> options)
        : DbContext(options)
    {
        public DbSet<SeoMetadataEntity> Pages => Set<SeoMetadataEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.ApplyGranitConventions();
    }

    private sealed class ComplexOwnerDbContext(DbContextOptions<ComplexOwnerDbContext> options)
        : DbContext(options)
    {
        public DbSet<ComplexOwnerEntity> Owners => Set<ComplexOwnerEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Opt out of jsonb: map the value object as a complex type (flat columns).
            modelBuilder.Entity<ComplexOwnerEntity>()
                .ComplexProperty(e => e.Location);

            modelBuilder.ApplyGranitConventions();
        }
    }

    private sealed class ExplicitOwnsOneDbContext(DbContextOptions<ExplicitOwnsOneDbContext> options)
        : DbContext(options)
    {
        public DbSet<OwnsOneOwnerEntity> Owners => Set<OwnsOneOwnerEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Unsupported third path: OwnsOne(...) on a ValueObject subtype with per-property
            // configuration. The convention must reject this at model build time.
            modelBuilder.Entity<OwnsOneOwnerEntity>()
                .OwnsOne(e => e.Location, loc =>
                {
                    loc.Property(g => g.Latitude).HasColumnName("lat");
                    loc.Property(g => g.Longitude).HasColumnName("lng");
                });

            modelBuilder.ApplyGranitConventions();
        }
    }
}
