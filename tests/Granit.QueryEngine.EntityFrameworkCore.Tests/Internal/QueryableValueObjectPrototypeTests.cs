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

    [Fact]
    public void Sort_translates_on_a_queryable_value_object()
    {
        QueryDefinitionBuilder<Site> builder = new();
        builder.Column(e => e.Slug, c => c.Sortable());

        using SiteCtx ctx = new(_options);
        List<string> ordered = [.. ctx.Sites.ApplySort("-slug", builder).Select(s => s.Slug.Value)];

        ordered.ShouldBe(["other", "my-shop", "my-blog"]);
    }

    [Fact]
    public async Task GroupBy_translates_on_a_queryable_value_object()
    {
        QueryDefinitionBuilder<Site> builder = new();
        builder.AllowGroupBy(e => e.Slug);

        await using SiteCtx ctx = new(_options);
        GroupedResult<Site> result = await ctx.Sites.ApplyGroupByAsync(nameof(Site.Slug), builder, 100, TestContext.Current.CancellationToken);

        result.Groups.Select(g => (string)g.Value!).ShouldBe(["my-blog", "my-shop", "other"], ignoreOrder: true);
        result.Groups.ShouldAllBe(g => g.Count == 1);
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            // The [QueryableValueObject] attribute alone drives the ComplexProperty mapping
            // (inner Value as the "Slug" column) — ApplyGranitConventions does it, no manual config.
            => modelBuilder.ApplyGranitConventions();
    }
}
