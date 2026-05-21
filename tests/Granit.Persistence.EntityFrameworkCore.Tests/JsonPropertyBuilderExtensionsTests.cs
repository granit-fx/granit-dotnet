// =============================================================================
// Tests - JsonPropertyBuilderExtensions
// =============================================================================
// Verifies that HasJsonConversion:
//   - Persists arbitrary CLR types as JSON strings (round-trip through EF Core).
//   - Wires a deep-equality ValueComparer so change tracking detects mutations
//     to JSON payloads (mutable collections, POCOs, JsonElement).
//   - Snapshots produce independent copies (snapshot mutation does not bleed).
// =============================================================================

using System.Text.Json;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class JsonPropertyBuilderExtensionsTests
{
    private sealed class ListEntity
    {
        public int Id { get; set; }
        public List<string> Tags { get; set; } = [];
    }

    private sealed class JsonElementEntity
    {
        public int Id { get; set; }
        public JsonElement Payload { get; set; }
    }

    private sealed class TestDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ListEntity> Lists => Set<ListEntity>();
        public DbSet<JsonElementEntity> JsonElements => Set<JsonElementEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ListEntity>().Property(e => e.Tags).HasJsonConversion();
            modelBuilder.Entity<JsonElementEntity>().Property(e => e.Payload).HasJsonConversion();
        }
    }

    private static TestDbContext NewContext() =>
        new(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    [Fact]
    public void HasJsonConversion_throws_on_null_builder() =>
        Should.Throw<ArgumentNullException>(
            () => ((Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<int>)null!).HasJsonConversion());

    [Fact]
    public async Task List_round_trips_through_json()
    {
        await using TestDbContext ctx = NewContext();
        ListEntity entity = new() { Id = 1, Tags = ["a", "b", "c"] };
        ctx.Lists.Add(entity);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        ctx.ChangeTracker.Clear();
        ListEntity? reloaded = await ctx.Lists.FindAsync([1], TestContext.Current.CancellationToken);

        reloaded.ShouldNotBeNull();
        reloaded.Tags.ShouldBe(["a", "b", "c"]);
    }

    [Fact]
    public async Task JsonElement_round_trips_through_json()
    {
        await using TestDbContext ctx = NewContext();
        JsonElement payload = JsonDocument.Parse("""{"k":"v","n":42}""").RootElement;
        ctx.JsonElements.Add(new JsonElementEntity { Id = 1, Payload = payload });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        ctx.ChangeTracker.Clear();
        JsonElementEntity? reloaded = await ctx.JsonElements.FindAsync([1], TestContext.Current.CancellationToken);

        reloaded.ShouldNotBeNull();
        reloaded.Payload.GetProperty("k").GetString().ShouldBe("v");
        reloaded.Payload.GetProperty("n").GetInt32().ShouldBe(42);
    }

    [Fact]
    public void ValueComparer_detects_list_mutation()
    {
        using TestDbContext ctx = NewContext();
        IProperty property = ctx.Model.FindEntityType(typeof(ListEntity))!
            .FindProperty(nameof(ListEntity.Tags))!;
        ValueComparer comparer = property.GetValueComparer();

        List<string> original = ["a", "b"];
        List<string> mutated = ["a", "b", "c"];

        comparer.Equals(original, original).ShouldBeTrue();
        comparer.Equals(original, mutated).ShouldBeFalse();
    }

    [Fact]
    public void Snapshot_is_an_independent_copy()
    {
        using TestDbContext ctx = NewContext();
        IProperty property = ctx.Model.FindEntityType(typeof(ListEntity))!
            .FindProperty(nameof(ListEntity.Tags))!;
        ValueComparer comparer = property.GetValueComparer();

        List<string> original = ["a", "b"];
        object snapshot = comparer.Snapshot(original)!;
        original.Add("c");

        ((List<string>)snapshot).ShouldBe(["a", "b"]);
    }
}
