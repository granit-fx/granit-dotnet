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

// ADR-070 strategy D: a NULLABLE [QueryableValueObject(Json)] column is auto-mapped (by
// ApplyGranitConventions) as a JSON column via OwnsOne().ToJson(). It round-trips null AND the
// QueryEngine drills .Value to translate substring search/filter — the nullable+searchable case
// that ComplexProperty (strategy B) cannot cover. End-to-end against real SQLite.
public sealed class QueryableValueObjectJsonStorageTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<PageCtx> _options;

    public QueryableValueObjectJsonStorageTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<PageCtx>().UseSqlite(_connection).Options;

        using PageCtx seed = new(_options);
        seed.Database.EnsureCreated();
        seed.Pages.AddRange(
            new Page { Id = Guid.NewGuid(), Slug = Slug.Create("my-blog") },
            new Page { Id = Guid.NewGuid(), Slug = Slug.Create("my-shop") },
            new Page { Id = Guid.NewGuid(), Slug = null });
        seed.SaveChanges();
    }

    [Fact]
    public void Nullable_json_value_object_round_trips_null()
    {
        using PageCtx ctx = new(_options);

        ctx.Pages.Count(p => p.Slug == null).ShouldBe(1);
        ctx.Pages.Count(p => p.Slug != null).ShouldBe(2);
    }

    [Fact]
    public void Global_search_drills_into_value_over_json()
    {
        using PageCtx ctx = new(_options);

        IQueryable<Page> filtered = new ContainsSearchStrategy<Page>()
            .ApplySearch(ctx.Pages, "my", [nameof(Page.Slug)]);

        filtered.Select(p => p.Slug!.Value).ToList().ShouldBe(["my-blog", "my-shop"], ignoreOrder: true);
    }

    [Fact]
    public void Contains_filter_translates_over_json()
    {
        Expression<Func<Page, bool>>? expr =
            FilterExpressionBuilder.Build<Page>(new(nameof(Page.Slug), FilterOperator.Contains, "shop"));
        expr.ShouldNotBeNull();

        using PageCtx ctx = new(_options);
        ctx.Pages.Where(expr).Select(p => p.Slug!.Value).ToList().ShouldBe(["my-shop"]);
    }

    public void Dispose() => _connection.Dispose();

    private sealed class Slug : SingleValueObject<string>
    {
        public override required string Value { get; init; }
        public static Slug Create(string v) => new() { Value = v };
        public static implicit operator string(Slug s) => s.Value;
    }

    private sealed class Page
    {
        public Guid Id { get; set; }

        [QueryableValueObject(QueryableValueObjectStorage.Json)]
        public Slug? Slug { get; set; }
    }

    private sealed class PageCtx(DbContextOptions<PageCtx> options) : DbContext(options)
    {
        public DbSet<Page> Pages => Set<Page>();
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyGranitConventions();
    }
}
