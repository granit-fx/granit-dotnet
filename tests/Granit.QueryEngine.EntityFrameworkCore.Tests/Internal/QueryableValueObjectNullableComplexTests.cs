using Granit.Domain;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

// ADR-070: a NULLABLE [QueryableValueObject] (default ComplexProperty storage) is supported via an
// EF shadow discriminator (EF requires it for an all-optional complex type). The .Value column
// stays a real, indexable scalar — unlike the JSON strategy. It round-trips null and the QueryEngine
// drills .Value for substring search.
public sealed class QueryableValueObjectNullableComplexTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<Ctx> _options;

    public QueryableValueObjectNullableComplexTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<Ctx>().UseSqlite(_connection).Options;

        using Ctx seed = new(_options);
        seed.Database.EnsureCreated();
        seed.Things.AddRange(
            new Thing { Id = Guid.NewGuid(), Maybe = MaybeSlug.Create("my-blog") },
            new Thing { Id = Guid.NewGuid(), Maybe = null });
        seed.SaveChanges();
    }

    [Fact]
    public void Nullable_complex_property_round_trips_null()
    {
        using Ctx ctx = new(_options);

        ctx.Things.Count(t => t.Maybe == null).ShouldBe(1);
        ctx.Things.Count(t => t.Maybe != null).ShouldBe(1);
    }

    [Fact]
    public void Search_drills_into_value_on_a_nullable_complex_property()
    {
        using Ctx ctx = new(_options);

        IQueryable<Thing> filtered = new ContainsSearchStrategy<Thing>()
            .ApplySearch(ctx.Things, "blog", [nameof(Thing.Maybe)]);

        filtered.Select(t => t.Maybe!.Value).ToList().ShouldBe(["my-blog"]);
    }

    public void Dispose() => _connection.Dispose();

    private sealed class MaybeSlug : SingleValueObject<string>
    {
        public override required string Value { get; init; }
        public static MaybeSlug Create(string v) => new() { Value = v };
        public static implicit operator string(MaybeSlug s) => s.Value;
    }

    private sealed class Thing
    {
        public Guid Id { get; set; }

        [QueryableValueObject]
        public MaybeSlug? Maybe { get; set; }
    }

    private sealed class Ctx(DbContextOptions<Ctx> options) : DbContext(options)
    {
        public DbSet<Thing> Things => Set<Thing>();
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyGranitConventions();
    }
}
