using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

public sealed class QueryEngineProjectionTests : IAsyncLifetime
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
                .DefaultPageSize(10)
                .MaxPageSize(50)
                .DefaultSort("-Price");
    }

    private sealed class CursorProductQueryDefinition : QueryDefinition<TestProduct>
    {
        public override string Name => "Test.Products.Cursor";

        protected override void Configure(QueryDefinitionBuilder<TestProduct> builder) =>
            builder
                .Column(p => p.Name, c => c.Label("Name").Sortable().Filterable())
                .Column(p => p.Price, c => c.Label("Price").Sortable().Filterable())
                .SupportsCursorPagination(p => p.Id)
                .DefaultPageSize(2)
                .MaxPageSize(10);
    }

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new TestDbContext(options);

        _db.Products.AddRange(
            new TestProduct { Id = Guid.NewGuid(), Name = "Laptop", Price = 1000, Activated = true, Category = ProductCategory.Electronics },
            new TestProduct { Id = Guid.NewGuid(), Name = "Novel", Price = 15, Activated = true, Category = ProductCategory.Books },
            new TestProduct { Id = Guid.NewGuid(), Name = "T-Shirt", Price = 25, Activated = true, Category = ProductCategory.Clothing },
            new TestProduct { Id = Guid.NewGuid(), Name = "Phone", Price = 800, Activated = true, Category = ProductCategory.Electronics },
            new TestProduct { Id = Guid.NewGuid(), Name = "Headset", Price = 200, Activated = true, Category = ProductCategory.Electronics });

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // Offset pagination with projection
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_with_projection_returns_projected_items()
    {
        ProductQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        PagedResult<ProductProjection> result = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { PageSize = 10 },
            p => new ProductProjection(p.Name, p.Price),
            TestContext.Current.CancellationToken);

        result.Items.Count.ShouldBe(5);
        result.Items.ShouldAllBe(p => !string.IsNullOrEmpty(p.Name));
    }

    [Fact]
    public async Task ExecuteAsync_with_projection_applies_offset_pagination()
    {
        ProductQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        PagedResult<ProductProjection> page1 = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { Page = 1, PageSize = 2 },
            p => new ProductProjection(p.Name, p.Price),
            TestContext.Current.CancellationToken);

        PagedResult<ProductProjection> page2 = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { Page = 2, PageSize = 2 },
            p => new ProductProjection(p.Name, p.Price),
            TestContext.Current.CancellationToken);

        page1.Items.Count.ShouldBe(2);
        page2.Items.Count.ShouldBe(2);
        page1.TotalCount.ShouldBe(5);
        page1.HasMore.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_with_projection_applies_filters()
    {
        ProductQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        PagedResult<ProductProjection> result = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest
            {
                Filter = new Dictionary<string, string> { ["Price.gte"] = "500" },
            },
            p => new ProductProjection(p.Name, p.Price),
            TestContext.Current.CancellationToken);

        result.Items.ShouldAllBe(p => p.Price >= 500);
        result.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_with_projection_skip_total_count()
    {
        ProductQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        PagedResult<ProductProjection> result = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { PageSize = 2, SkipTotalCount = true },
            p => new ProductProjection(p.Name, p.Price),
            TestContext.Current.CancellationToken);

        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBeNull();
        result.HasMore.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_with_projection_throws_on_null_projection()
    {
        ProductQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        await Should.ThrowAsync<ArgumentNullException>(() =>
            engine.ExecuteAsync<ProductProjection>(
                _db.Products.AsQueryable(),
                new QueryRequest(),
                null!,
                TestContext.Current.CancellationToken));
    }

    // -------------------------------------------------------------------------
    // Cursor pagination with projection
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_with_projection_cursor_pagination()
    {
        CursorProductQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        // Get first page items to build cursor
        PagedResult<TestProduct> firstPage = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { PageSize = 2 },
            TestContext.Current.CancellationToken);

        // Use cursor for next page with projection
        if (firstPage.NextCursor is not null)
        {
            PagedResult<ProductProjection> nextPage = await engine.ExecuteAsync(
                _db.Products.AsQueryable(),
                new QueryRequest { Cursor = firstPage.NextCursor, PageSize = 2 },
                p => new ProductProjection(p.Name, p.Price),
                TestContext.Current.CancellationToken);

            nextPage.Items.ShouldNotBeEmpty();
            nextPage.TotalCount.ShouldBeNull();
        }
    }

    private sealed record ProductProjection(string Name, int Price);
}
