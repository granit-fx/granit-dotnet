using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Granit.QueryEngine.Filtering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

public sealed class QueryableGroupByAggregatesTests : IAsyncLifetime
{
    private TestDbContext _db = null!;

    private sealed class AggregateDefinition : QueryDefinition<TestProduct>
    {
        public override string Name => "Test.AggregateProducts";

        protected override void Configure(QueryDefinitionBuilder<TestProduct> builder) =>
            builder
                .Column(p => p.Name, c => c.Label("Name"))
                .Column(p => p.Category, c => c.Label("Category"))
                .AllowGroupBy(p => p.Category)
                .Aggregate(p => p.Price, AggregateFunction.Sum, "totalPrice")
                .Aggregate(p => p.Price, AggregateFunction.Avg, "avgPrice")
                .Aggregate(p => p.Price, AggregateFunction.Min, "minPrice")
                .Aggregate(p => p.Price, AggregateFunction.Max, "maxPrice")
                .Aggregate(p => p.Name, AggregateFunction.Count, "productCount");
    }

    private sealed class NoAggregateDefinition : QueryDefinition<TestProduct>
    {
        public override string Name => "Test.NoAggregateProducts";

        protected override void Configure(QueryDefinitionBuilder<TestProduct> builder) =>
            builder
                .Column(p => p.Name, c => c.Label("Name"))
                .AllowGroupBy(p => p.Category);
    }

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new TestDbContext(options);

        _db.Products.AddRange(
            new TestProduct { Id = Guid.NewGuid(), Name = "Laptop", Price = 1000, Activated = true, Category = ProductCategory.Electronics },
            new TestProduct { Id = Guid.NewGuid(), Name = "Phone", Price = 800, Activated = true, Category = ProductCategory.Electronics },
            new TestProduct { Id = Guid.NewGuid(), Name = "Headset", Price = 200, Activated = false, Category = ProductCategory.Electronics },
            new TestProduct { Id = Guid.NewGuid(), Name = "Novel", Price = 15, Activated = true, Category = ProductCategory.Books });

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private static QueryEngine<TestProduct> CreateEngine(QueryDefinition<TestProduct> definition) =>
        new(definition,
            NullLogger<QueryEngine<TestProduct>>.Instance,
            Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

    [Fact]
    public async Task Declared_aggregates_are_computed_per_group()
    {
        QueryEngine<TestProduct> engine = CreateEngine(new AggregateDefinition());

        GroupedResult<TestProduct> result = await engine.ExecuteGroupedAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { GroupBy = "Category" },
            TestContext.Current.CancellationToken);

        GroupEntry<TestProduct> electronics = result.Groups.First(g => g.Label == "Electronics");
        electronics.Aggregates.ShouldNotBeNull();
        electronics.Aggregates!["totalPrice"].ShouldBe(2000m);
        ((decimal)electronics.Aggregates["avgPrice"]!).ShouldBe(2000m / 3, 0.01m);
        electronics.Aggregates["minPrice"].ShouldBe(200m);
        electronics.Aggregates["maxPrice"].ShouldBe(1000m);
        electronics.Aggregates["productCount"].ShouldBe(3m);

        GroupEntry<TestProduct> books = result.Groups.First(g => g.Label == "Books");
        books.Aggregates.ShouldNotBeNull();
        books.Aggregates!["totalPrice"].ShouldBe(15m);
        books.Aggregates["productCount"].ShouldBe(1m);
    }

    [Fact]
    public async Task Aggregates_flow_through_projected_grouped_results()
    {
        QueryEngine<TestProduct> engine = CreateEngine(new AggregateDefinition());

        GroupedResult<ProductSummary> result = await engine.ExecuteGroupedAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { GroupBy = "Category" },
            p => new ProductSummary(p.Name, p.Price),
            TestContext.Current.CancellationToken);

        GroupEntry<ProductSummary> electronics = result.Groups.First(g => g.Label == "Electronics");
        electronics.Aggregates.ShouldNotBeNull();
        electronics.Aggregates!["totalPrice"].ShouldBe(2000m);
    }

    [Fact]
    public async Task Without_declared_aggregates_the_dictionary_stays_null()
    {
        QueryEngine<TestProduct> engine = CreateEngine(new NoAggregateDefinition());

        GroupedResult<TestProduct> result = await engine.ExecuteGroupedAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { GroupBy = "Category" },
            TestContext.Current.CancellationToken);

        result.Groups.ShouldAllBe(g => g.Aggregates == null);
    }

    private sealed record ProductSummary(string Name, int Price);
}
