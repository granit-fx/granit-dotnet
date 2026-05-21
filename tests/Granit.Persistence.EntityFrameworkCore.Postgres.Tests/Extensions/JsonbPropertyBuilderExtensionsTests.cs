// =============================================================================
// Tests - JsonbPropertyBuilderExtensions
// =============================================================================
// Verifies that HasJsonbConversion:
//   - Sets the EF Core column type to "jsonb"
//   - Wires the same converter + comparer as HasJsonConversion (delegated)
//   - Guards against null builder
// =============================================================================

using Granit.Persistence.EntityFrameworkCore.Postgres.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Postgres.Tests.Extensions;

public sealed class JsonbPropertyBuilderExtensionsTests
{
    private sealed class JsonbEntity
    {
        public int Id { get; set; }
        public List<string> Tags { get; set; } = [];
    }

    private sealed class TestDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<JsonbEntity> Items => Set<JsonbEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<JsonbEntity>().Property(e => e.Tags).HasJsonbConversion();
    }

    // Connection string is never used: building the model and reading metadata does not
    // open a connection, so the tests run fully offline.
    private static IProperty TagsProperty()
    {
        TestDbContext ctx = new(new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql("Host=fake;Database=fake")
            .Options);
        return ctx.Model.FindEntityType(typeof(JsonbEntity))!.FindProperty(nameof(JsonbEntity.Tags))!;
    }

    [Fact]
    public void HasJsonbConversion_throws_on_null_builder() =>
        Should.Throw<ArgumentNullException>(
            () => ((PropertyBuilder<int>)null!).HasJsonbConversion());

    [Fact]
    public void HasJsonbConversion_sets_column_type_to_jsonb() =>
        TagsProperty().GetColumnType().ShouldBe("jsonb");

    [Fact]
    public void HasJsonbConversion_wires_value_converter()
    {
        IProperty property = TagsProperty();

        property.GetValueConverter().ShouldNotBeNull();
    }

    [Fact]
    public void HasJsonbConversion_wires_deep_equality_comparer()
    {
        ValueComparer comparer = TagsProperty().GetValueComparer();

        comparer.Equals(new List<string> { "a", "b" }, new List<string> { "a", "b" }).ShouldBeTrue();
        comparer.Equals(new List<string> { "a", "b" }, new List<string> { "a", "b", "c" }).ShouldBeFalse();
    }
}
