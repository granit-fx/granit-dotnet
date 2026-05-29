// =============================================================================
// ValueObject removal under the RELATIONAL provider (nested / explicit / collection)
// =============================================================================
// Companion to the in-memory ValueObjectEntityAutoDiscoveryRemovalTests, exercising
// the convention against the Npgsql provider with a fake connection string — model
// building never opens a connection, so no database is needed.
//
// Context: in granit-website's real SeoDbContext, RemoveEntityType alone proved
// insufficient. An earlier ApplyGranitConventions pass that calls
// modelBuilder.Entity<TOwner>() (e.g. the IConcurrencyAware pass) re-fires EF
// navigation discovery, and relational model FINALIZATION re-materialises the
// phantom value object graph (OpenGraph → OgImage → ImageDimensions) off the
// still-present CLR navigation properties, failing to bind
// ImageDimensions(int width, int height). A model-level modelBuilder.Ignore(type)
// keeps multi-field value objects out for good; SingleValueObject<T> stays a
// removable scalar so its primitive converter survives.
//
// NOTE: that finalization crash could not be minimised into this project (it needs
// the full SeoDbContext shape). The authoritative red→green repro lives in
// granit-website (Docker-free SeoModelBuildTests). These tests instead lock in the
// post-fix relational behaviour — no value object resurrected as an entity, and the
// jsonb columns intact — across the nested, explicit-config, and collection cases,
// guarding against regressions in the convention.
// =============================================================================

using Granit.Domain;
using Granit.Domain.ValueObjects;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.Postgres.Extensions;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Postgres.Tests;

public sealed class ValueObjectFinalizationRemovalTests
{
    [Fact]
    public void RelationalModelBuild_EntityWithNestedValueObject_DoesNotResurrectPhantomEntities()
    {
        // Act — accessing the model triggers full relational finalization (the phase that, in
        // the real SeoDbContext, re-materialised the phantom value object graph).
        using SeoDbContext context = new(new DbContextOptionsBuilder<SeoDbContext>()
            .UseNpgsql("Host=fake;Database=fake")
            .Options);

        IReadOnlyList<string> entityNames = [.. context.Model.GetEntityTypes()
            .Select(et => et.ClrType.Name)];

        // Assert — no value object survives as an entity type after finalization.
        entityNames.ShouldContain(nameof(SeoMetadataEntity), "the owner entity must remain");
        entityNames.ShouldNotContain(nameof(TestOpenGraph));
        entityNames.ShouldNotContain(nameof(TestOgImage));
        entityNames.ShouldNotContain(nameof(ImageDimensions));
        entityNames.ShouldNotContain(nameof(TestHreflang));

        // Assert — both the nested-graph property and the value object collection survive as
        // jsonb columns.
        Microsoft.EntityFrameworkCore.Metadata.IEntityType owner =
            context.Model.FindEntityType(typeof(SeoMetadataEntity))!;

        owner.FindProperty(nameof(SeoMetadataEntity.OpenGraph))
            .ShouldNotBeNull("the value object must survive as a JSON column")
            .GetColumnType().ShouldBe("jsonb", "the nested value object column must be jsonb");

        owner.FindProperty(nameof(SeoMetadataEntity.Alternates))
            .ShouldNotBeNull("the value object collection must survive as a JSON column")
            .GetColumnType().ShouldBe("jsonb", "the value object collection column must be jsonb");
    }

    [Fact]
    public void RelationalModelBuild_EntityWithExplicitlyConvertedValueObject_DoesNotResurrectNestedPhantoms()
    {
        // Mirrors the real SEO module: the owner declares its own jsonb HasJsonConversion on
        // the top-level value object. EF still discovered the NESTED value objects (OgImage,
        // ImageDimensions) as phantom entity types before the converter applied; only the
        // type-level Ignore keeps them out through finalization.
        using ExplicitSeoDbContext context = new(new DbContextOptionsBuilder<ExplicitSeoDbContext>()
            .UseNpgsql("Host=fake;Database=fake")
            .Options);

        IReadOnlyList<string> entityNames = [.. context.Model.GetEntityTypes()
            .Select(et => et.ClrType.Name)];

        entityNames.ShouldContain(nameof(SeoMetadataEntity));
        entityNames.ShouldNotContain(nameof(TestOpenGraph));
        entityNames.ShouldNotContain(nameof(TestOgImage));
        entityNames.ShouldNotContain(nameof(ImageDimensions));

        context.Model.FindEntityType(typeof(SeoMetadataEntity))!
            .FindProperty(nameof(SeoMetadataEntity.OpenGraph))!
            .GetColumnType().ShouldBe("jsonb");
    }

    // ImageDimensions (real framework VO) is the leaf — its parameterized ctor is exactly
    // what EF could not bind when the type was wrongly resurrected as an entity.
    public sealed class TestOgImage(string url, ImageDimensions dimensions) : ValueObject
    {
        public string Url { get; } = url;
        public ImageDimensions Dimensions { get; } = dimensions;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Url;
            yield return Dimensions;
        }
    }

    public sealed class TestOpenGraph(string title, TestOgImage image) : ValueObject
    {
        public string Title { get; } = title;
        public TestOgImage Image { get; } = image;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Title;
            yield return Image;
        }
    }

    // IConcurrencyAware makes ApplyGranitConventions call Entity<SeoMetadataEntity>() early,
    // which is the trigger that lets finalization re-discover the phantom value object graph.
    public sealed class TestHreflang(string culture, string url) : ValueObject
    {
        public string Culture { get; } = culture;
        public string Url { get; } = url;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Culture;
            yield return Url;
        }
    }

    public sealed class SeoMetadataEntity : IConcurrencyAware
    {
        public Guid Id { get; set; }

        // No explicit configuration — relies entirely on the framework convention.
        public TestOpenGraph? OpenGraph { get; set; }

        // A collection of value objects — EF discovers TestHreflang as a collection navigation.
        public IReadOnlyList<TestHreflang> Alternates { get; set; } = [];

        public string ConcurrencyStamp { get; set; } = string.Empty;
    }

    private sealed class SeoDbContext(DbContextOptions<SeoDbContext> options) : DbContext(options)
    {
        public DbSet<SeoMetadataEntity> Pages => Set<SeoMetadataEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyGranitConventions();
            modelBuilder.UseGranitJsonbForJsonProperties();
        }
    }

    private sealed class ExplicitSeoDbContext(DbContextOptions<ExplicitSeoDbContext> options) : DbContext(options)
    {
        public DbSet<SeoMetadataEntity> Pages => Set<SeoMetadataEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // The module configures the top-level value object itself (as the real SEO module does).
            modelBuilder.Entity<SeoMetadataEntity>()
                .Property(e => e.OpenGraph)
                .HasJsonConversion();

            modelBuilder.ApplyGranitConventions();
            modelBuilder.UseGranitJsonbForJsonProperties();
        }
    }
}
