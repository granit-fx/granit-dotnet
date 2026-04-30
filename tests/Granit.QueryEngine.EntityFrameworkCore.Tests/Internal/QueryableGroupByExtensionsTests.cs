using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

public sealed class QueryableGroupByExtensionsTests : IAsyncLifetime
{
    private TestDbContext _db = null!;

    private sealed class GroupByDefinition : QueryDefinition<TestProduct>
    {
        public override string Name => "Test.GroupByProducts";

        protected override void Configure(QueryDefinitionBuilder<TestProduct> builder) =>
            builder
                .Column(p => p.Name, c => c.Label("Name"))
                .Column(p => p.Category, c => c.Label("Category"))
                .Column(p => p.Activated, c => c.Label("Active"))
                .AllowGroupBy(p => p.Category)
                .AllowGroupBy(p => p.Activated);
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
            new TestProduct { Id = Guid.NewGuid(), Name = "Novel", Price = 15, Activated = true, Category = ProductCategory.Books },
            new TestProduct { Id = Guid.NewGuid(), Name = "T-Shirt", Price = 25, Activated = true, Category = ProductCategory.Clothing });

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GroupBy_enum_field_returns_groups_with_counts()
    {
        QueryEngine<TestProduct> engine = new(new GroupByDefinition(), NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        GroupedResult<TestProduct> result = await engine.ExecuteGroupedAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { GroupBy = "Category" },
            TestContext.Current.CancellationToken);

        result.Groups.Count.ShouldBe(3);
        result.TotalCount.ShouldBe(5);

        GroupEntry<TestProduct> electronics = result.Groups.First(g => g.Label == "Electronics");
        electronics.Count.ShouldBe(3);
        electronics.Field.ShouldBe("Category");
    }

    [Fact]
    public async Task GroupBy_bool_field_returns_groups()
    {
        QueryEngine<TestProduct> engine = new(new GroupByDefinition(), NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        GroupedResult<TestProduct> result = await engine.ExecuteGroupedAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { GroupBy = "Activated" },
            TestContext.Current.CancellationToken);

        result.Groups.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(5);

        GroupEntry<TestProduct> active = result.Groups.First(g => g.Label == "True");
        active.Count.ShouldBe(4);
    }

    [Fact]
    public async Task GroupBy_null_or_empty_returns_empty()
    {
        QueryEngine<TestProduct> engine = new(new GroupByDefinition(), NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        GroupedResult<TestProduct> result = await engine.ExecuteGroupedAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { GroupBy = null },
            TestContext.Current.CancellationToken);

        result.Groups.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task GroupBy_non_whitelisted_field_returns_empty()
    {
        QueryEngine<TestProduct> engine = new(new GroupByDefinition(), NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        // Price exists on TestProduct but is NOT in AllowGroupBy
        GroupedResult<TestProduct> result = await engine.ExecuteGroupedAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { GroupBy = "Price" },
            TestContext.Current.CancellationToken);

        result.Groups.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task GroupBy_unknown_field_returns_empty()
    {
        QueryEngine<TestProduct> engine = new(new GroupByDefinition(), NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        GroupedResult<TestProduct> result = await engine.ExecuteGroupedAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { GroupBy = "NonExistent" },
            TestContext.Current.CancellationToken);

        result.Groups.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task GroupBy_with_projection_returns_projected_items()
    {
        QueryEngine<TestProduct> engine = new(new GroupByDefinition(), NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        GroupedResult<ProductSummary> result = await engine.ExecuteGroupedAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { GroupBy = "Category" },
            p => new ProductSummary(p.Name, p.Price),
            TestContext.Current.CancellationToken);

        result.Groups.Count.ShouldBe(3);
        result.TotalCount.ShouldBe(5);

        GroupEntry<ProductSummary> electronics = result.Groups.First(g => g.Label == "Electronics");
        electronics.Count.ShouldBe(3);
        electronics.Items.ShouldNotBeNull();
        electronics.Items!.Count.ShouldBe(3);
        electronics.Items.ShouldAllBe(item => item is ProductSummary);
        electronics.Items.Select(i => i.Name).ShouldContain("Laptop");
    }

    [Fact]
    public async Task GroupBy_with_projection_and_empty_groupBy_returns_empty()
    {
        QueryEngine<TestProduct> engine = new(new GroupByDefinition(), NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        GroupedResult<ProductSummary> result = await engine.ExecuteGroupedAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { GroupBy = null },
            p => new ProductSummary(p.Name, p.Price),
            TestContext.Current.CancellationToken);

        result.Groups.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    private sealed record ProductSummary(string Name, decimal Price);
}
