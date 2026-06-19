using System.Linq.Expressions;
using Granit.Domain;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Granit.QueryEngine.Filtering;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

// Issue #2767/#2770: end-to-end proof against real SQLite + the real SingleValueObjectConverter
// (wired by ApplyGranitConventions) that filtering/sorting a SingleValueObject<string> column is
// safe. Equality/IN translate through the converter; substring/range operators are dropped (never
// silently mistranslated or thrown); sorting translates. Nothing else in the suite exercises EF
// translation for value-object columns, so this guards the core promise of the fix.
public sealed class ValueObjectColumnTranslationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<VoCtx> _options;

    public ValueObjectColumnTranslationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<VoCtx>().UseSqlite(_connection).Options;

        using VoCtx seed = new(_options);
        seed.Database.EnsureCreated();
        seed.Probes.AddRange(
            new VoProbe { Id = Guid.NewGuid(), Slug = Slug.Create("alpha") },
            new VoProbe { Id = Guid.NewGuid(), Slug = Slug.Create("beta") },
            new VoProbe { Id = Guid.NewGuid(), Slug = Slug.Create("gamma") });
        seed.SaveChanges();
    }

    [Fact]
    public void Eq_filter_translates_and_matches_the_value() =>
        Query(new("Slug", FilterOperator.Eq, "beta")).ShouldBe(["beta"]);

    [Fact]
    public void In_filter_translates_to_sql_in()
    {
        Query(new("Slug", FilterOperator.In, "alpha,gamma"))
            .ShouldBe(["alpha", "gamma"], ignoreOrder: true);
    }

    [Theory]
    [InlineData(FilterOperator.Contains)]
    [InlineData(FilterOperator.StartsWith)]
    [InlineData(FilterOperator.EndsWith)]
    [InlineData(FilterOperator.Gt)]
    [InlineData(FilterOperator.Lt)]
    public void Unsupported_operators_are_dropped_not_thrown(FilterOperator op) =>
        FilterExpressionBuilder.Build<VoProbe>(new("Slug", op, "beta")).ShouldBeNull();

    [Fact]
    public void Sorting_by_a_value_object_column_translates()
    {
        using VoCtx ctx = new(_options);

        List<string> sorted = [.. ctx.Probes.OrderBy(e => e.Slug).Select(e => e.Slug.Value)];

        sorted.ShouldBe(["alpha", "beta", "gamma"]);
    }

    private List<string> Query(FilterCriteria criteria)
    {
        Expression<Func<VoProbe, bool>>? expr = FilterExpressionBuilder.Build<VoProbe>(criteria);
        expr.ShouldNotBeNull();

        using VoCtx ctx = new(_options);
        return [.. ctx.Probes.Where(expr).Select(e => e.Slug.Value)];
    }

    public void Dispose() => _connection.Dispose();

    private sealed class Slug : SingleValueObject<string>
    {
        public override required string Value { get; init; }
        public static Slug Create(string value) => new() { Value = value };
        public static implicit operator string(Slug s) => s.Value;
    }

    private sealed class VoProbe
    {
        public Guid Id { get; set; }
        public Slug Slug { get; set; } = null!;
    }

    private sealed class VoCtx(DbContextOptions<VoCtx> options) : DbContext(options)
    {
        public DbSet<VoProbe> Probes => Set<VoProbe>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.ApplyGranitConventions();
    }
}
