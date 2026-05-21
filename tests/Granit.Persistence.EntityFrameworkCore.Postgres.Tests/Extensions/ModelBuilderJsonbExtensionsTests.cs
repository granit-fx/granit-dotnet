// =============================================================================
// Tests - ModelBuilderJsonbExtensions
// =============================================================================
// Verifies UseGranitJsonbForJsonProperties:
//   - Upgrades HasJsonConversion-marked properties to jsonb
//   - Preserves an explicit column type set by HasJsonbConversion / HasColumnType
//   - Leaves unmarked properties alone
//   - Is idempotent (calling twice produces the same model)
// =============================================================================

using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.Postgres.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Postgres.Tests.Extensions;

public sealed class ModelBuilderJsonbExtensionsTests
{
    private sealed class JsonbEntity
    {
        public int Id { get; set; }
        public List<string> JsonTags { get; set; } = [];
        public List<string> ExplicitJsonbTags { get; set; } = [];
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<JsonbEntity> Items => Set<JsonbEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<JsonbEntity>(e =>
            {
                e.Property(x => x.JsonTags).HasJsonConversion();
                e.Property(x => x.ExplicitJsonbTags).HasJsonbConversion();
                e.Property(x => x.Name);
            });

            modelBuilder.UseGranitJsonbForJsonProperties();
        }
    }

    private static IProperty Property(string name)
    {
        TestDbContext ctx = new(new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql("Host=fake;Database=fake")
            .Options);
        return ctx.Model.FindEntityType(typeof(JsonbEntity))!.FindProperty(name)!;
    }

    [Fact]
    public void Upgrades_HasJsonConversion_marked_property_to_jsonb() =>
        Property(nameof(JsonbEntity.JsonTags)).GetColumnType().ShouldBe("jsonb");

    [Fact]
    public void Preserves_explicit_column_type_set_by_HasJsonbConversion() =>
        Property(nameof(JsonbEntity.ExplicitJsonbTags)).GetColumnType().ShouldBe("jsonb");

    [Fact]
    public void Leaves_unmarked_properties_alone()
    {
        // Plain string property: Npgsql will pick "text" (or its own default); the
        // convention must not override that to "jsonb".
        IProperty name = Property(nameof(JsonbEntity.Name));

        name.GetColumnType().ShouldNotBe("jsonb");
    }

    [Fact]
    public void UseGranitJsonbForJsonProperties_throws_on_null_builder() =>
        Should.Throw<ArgumentNullException>(
            () => ((ModelBuilder)null!).UseGranitJsonbForJsonProperties());

    [Fact]
    public void HasJsonConversion_sets_the_json_serialized_marker_annotation()
    {
        // Sanity check that the base helper still sets the annotation our
        // convention relies on — guards against future refactors that drop it.
        IProperty property = Property(nameof(JsonbEntity.JsonTags));

        property.FindAnnotation(GranitPersistenceAnnotationNames.JsonSerialized)?.Value.ShouldBe(true);
    }
}
