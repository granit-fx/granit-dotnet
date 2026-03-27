using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

public sealed class QueryEngineStreamTests : IAsyncLifetime
{
    private TestDbContext _db = null!;

    private sealed class ProductQueryDefinition : QueryDefinition<TestProduct>
    {
        public override string Name => "Test.Products";

        protected override void Configure(QueryDefinitionBuilder<TestProduct> builder) =>
            builder
                .Column(p => p.Name, c => c.Label("Name").Sortable().Filterable())
                .Column(p => p.Price, c => c.Label("Price").Sortable().Filterable())
                .Column(p => p.Category, c => c.Label("Category").Filterable())
                .GlobalSearch(p => p.Name)
                .FilterGroup("Category", g => g
                    .Preset("Electronics", p => p.Category == ProductCategory.Electronics, isDefault: true))
                .DefaultSort("-Price");
    }

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new TestDbContext(options);

        _db.Products.AddRange(
            new TestProduct { Id = Guid.NewGuid(), Name = "Laptop", Price = 1000, IsActive = true, Category = ProductCategory.Electronics },
            new TestProduct { Id = Guid.NewGuid(), Name = "Novel", Price = 15, IsActive = true, Category = ProductCategory.Books },
            new TestProduct { Id = Guid.NewGuid(), Name = "T-Shirt", Price = 25, IsActive = true, Category = ProductCategory.Clothing },
            new TestProduct { Id = Guid.NewGuid(), Name = "Phone", Price = 800, IsActive = true, Category = ProductCategory.Electronics },
            new TestProduct { Id = Guid.NewGuid(), Name = "Headset", Price = 200, IsActive = true, Category = ProductCategory.Electronics });

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ExecuteStreamAsync_returns_all_matching_items()
    {
        // Arrange
        ProductQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        // Act — default preset filters to Electronics (3 items)
        List<TestProduct> items = [];
        await foreach (TestProduct item in engine.ExecuteStreamAsync(
            _db.Products.AsQueryable(),
            new QueryRequest(),
            TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        // Assert — no pagination, all 3 Electronics items returned
        items.Count.ShouldBe(3);
        items.ShouldAllBe(p => p.Category == ProductCategory.Electronics);
    }

    [Fact]
    public async Task ExecuteStreamAsync_applies_filters()
    {
        // Arrange
        ProductQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        // Act
        List<TestProduct> items = [];
        await foreach (TestProduct item in engine.ExecuteStreamAsync(
            _db.Products.AsQueryable(),
            new QueryRequest
            {
                Filter = new Dictionary<string, string> { ["Price.gte"] = "500" },
                Presets = new Dictionary<string, string> { ["Category"] = "Electronics" },
            },
            TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        // Assert
        items.Count.ShouldBe(2);
        items.ShouldAllBe(p => p.Price >= 500);
    }

    [Fact]
    public async Task ExecuteStreamAsync_applies_sorting()
    {
        // Arrange
        ProductQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        // Act — sort ascending by price
        List<TestProduct> items = [];
        await foreach (TestProduct item in engine.ExecuteStreamAsync(
            _db.Products.AsQueryable(),
            new QueryRequest
            {
                Sort = "Price",
                Presets = new Dictionary<string, string> { ["Category"] = "Electronics" },
            },
            TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        // Assert — ascending order
        items.Count.ShouldBe(3);
        items[0].Price.ShouldBeLessThanOrEqualTo(items[1].Price);
        items[1].Price.ShouldBeLessThanOrEqualTo(items[2].Price);
    }

    [Fact]
    public async Task ExecuteStreamAsync_empty_request_returns_all_with_default_preset()
    {
        // Arrange
        ProductQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        // Act
        List<TestProduct> items = [];
        await foreach (TestProduct item in engine.ExecuteStreamAsync(
            _db.Products.AsQueryable(),
            new QueryRequest(),
            TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        // Assert — default preset filters to Electronics
        items.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ExecuteStreamAsync_applies_search()
    {
        // Arrange
        ProductQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        // Act
        List<TestProduct> items = [];
        await foreach (TestProduct item in engine.ExecuteStreamAsync(
            _db.Products.AsQueryable(),
            new QueryRequest
            {
                Search = "Laptop",
                Presets = new Dictionary<string, string> { ["Category"] = "Electronics" },
            },
            TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        // Assert
        items.Count.ShouldBe(1);
        items[0].Name.ShouldBe("Laptop");
    }

    [Fact]
    public async Task ExecuteStreamAsync_ignores_pagination()
    {
        // Arrange
        ProductQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        // Act — even with PageSize=1, stream should return all items
        List<TestProduct> items = [];
        await foreach (TestProduct item in engine.ExecuteStreamAsync(
            _db.Products.AsQueryable(),
            new QueryRequest
            {
                Page = 1,
                PageSize = 1,
                Presets = new Dictionary<string, string> { ["Category"] = "Electronics" },
            },
            TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        // Assert — all 3 Electronics items, pagination ignored
        items.Count.ShouldBe(3);
    }
}
