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

// Prototype for ADR-070 strategy B (#2767): a [QueryableValueObject] column is mapped as an EF
// ComplexProperty (so .Value is a real scalar column), and the QueryEngine drills into .Value —
// substring search and substring filters then translate, while a plain converter-mapped VO would
// be rejected/dropped. End-to-end against real SQLite, alongside ApplyGranitConventions.
public sealed class QueryableValueObjectPrototypeTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<SiteCtx> _options;

    public QueryableValueObjectPrototypeTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<SiteCtx>().UseSqlite(_connection).Options;

        using SiteCtx seed = new(_options);
        seed.Database.EnsureCreated();
        seed.Sites.AddRange(
            new Site { Id = Guid.NewGuid(), Slug = Slug.Create("my-blog") },
            new Site { Id = Guid.NewGuid(), Slug = Slug.Create("my-shop") },
            new Site { Id = Guid.NewGuid(), Slug = Slug.Create("other") });
        seed.SaveChanges();
    }

    [Fact]
    public void GlobalSearch_allows_a_queryable_value_object_column()
    {
        QueryDefinitionBuilder<Site> builder = new();

        Should.NotThrow(() => builder.GlobalSearch(e => e.Slug));
        builder.GlobalSearchProperties.ShouldContain(nameof(Site.Slug));
    }

    [Fact]
    public void Global_search_strategy_drills_into_value_and_translates_like()
    {
        using SiteCtx ctx = new(_options);

        IQueryable<Site> filtered = new ContainsSearchStrategy<Site>()
            .ApplySearch(ctx.Sites, "my", [nameof(Site.Slug)]);

        filtered.Select(s => s.Slug.Value).ToList()
            .ShouldBe(["my-blog", "my-shop"], ignoreOrder: true);
    }

    [Fact]
    public void Contains_filter_translates_on_a_queryable_value_object()
    {
        Query(new(nameof(Site.Slug), FilterOperator.Contains, "shop"))
            .ShouldBe(["my-shop"]);
    }

    [Fact]
    public void Eq_filter_still_works_on_a_queryable_value_object()
    {
        Query(new(nameof(Site.Slug), FilterOperator.Eq, "other"))
            .ShouldBe(["other"]);
    }

    private List<string> Query(FilterCriteria criteria)
    {
        Expression<Func<Site, bool>>? expr = FilterExpressionBuilder.Build<Site>(criteria);
        expr.ShouldNotBeNull();

        using SiteCtx ctx = new(_options);
        return [.. ctx.Sites.Where(expr).Select(s => s.Slug.Value)];
    }

    public void Dispose() => _connection.Dispose();

    private sealed class Slug : SingleValueObject<string>
    {
        public override required string Value { get; init; }
        public static Slug Create(string v) => new() { Value = v };
        public static implicit operator string(Slug s) => s.Value;
    }

    private sealed class Site
    {
        public Guid Id { get; set; }

        [QueryableValueObject]
        public Slug Slug { get; set; } = null!;
    }

    private sealed class SiteCtx(DbContextOptions<SiteCtx> options) : DbContext(options)
    {
        public DbSet<Site> Sites => Set<Site>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            // Strategy B mapping: VO as a complex property, inner Value named to match the column.
            b.Entity<Site>().ComplexProperty(e => e.Slug, cb => cb.Property(s => s.Value).HasColumnName("Slug"));
            // Coexists with the framework conventions (the complex property is not a scalar
            // property nor an entity type, so the SVO converter/removal passes leave it alone).
            b.ApplyGranitConventions();
        }
    }
}
