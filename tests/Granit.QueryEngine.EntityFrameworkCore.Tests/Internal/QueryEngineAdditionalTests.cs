using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Granit.QueryEngine.Filtering;
using Granit.QueryEngine.Meta;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

public sealed class QueryEngineAdditionalTests : IAsyncLifetime
{
    private TestDbContext _db = null!;

    private sealed class FullQueryDefinition : QueryDefinition<TestProduct>
    {
        public override string Name => "Test.FullProducts";

        protected override void Configure(QueryDefinitionBuilder<TestProduct> builder) =>
            builder
                .Column(p => p.Name, c => c.Label("Name").Sortable().Filterable())
                .Column(p => p.Price, c => c.Label("Price").Sortable().Filterable())
                .Column(p => p.Category, c => c.Label("Category").Filterable())
                .Column(p => p.IsActive, c => c.Label("Active").Filterable().Visible(false))
                .GlobalSearch(p => p.Name)
                .DateFilter(p => p.CreatedAt, DatePeriod.ThisMonth)
                .AllowGroupBy(p => p.Category)
                .Aggregate(p => p.Price, AggregateFunction.Sum, "totalPrice")
                .QuickFilter("Active", p => p.IsActive, isDefault: true)
                .SupportsCursorPagination(p => p.Id)
                .DefaultPageSize(10)
                .MaxPageSize(50)
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
            new TestProduct { Id = Guid.NewGuid(), Name = "T-Shirt", Price = 25, IsActive = false, Category = ProductCategory.Clothing },
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
    public async Task ExecuteAsync_SkipTotalCount_returns_null_totalCount()
    {
        FullQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance);

        PagedResult<TestProduct> result = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { PageSize = 2, SkipTotalCount = true },
            TestContext.Current.CancellationToken);

        result.TotalCount.ShouldBeNull();
        result.Items.Count.ShouldBe(2);
        result.HasMore.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_negative_page_is_clamped_to_1()
    {
        FullQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance);

        PagedResult<TestProduct> result = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { Page = -5, PageSize = 10 },
            TestContext.Current.CancellationToken);

        // Should behave as page 1
        result.Items.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_null_page_defaults_to_1()
    {
        FullQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance);

        PagedResult<TestProduct> result = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { PageSize = 10 },
            TestContext.Current.CancellationToken);

        result.Items.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_null_pageSize_uses_default()
    {
        FullQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance);

        PagedResult<TestProduct> result = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest(),
            TestContext.Current.CancellationToken);

        // Default page size is 10, and we have 4 active products
        result.Items.Count.ShouldBeLessThanOrEqualTo(10);
    }

    [Fact]
    public async Task ExecuteAsync_zero_pageSize_uses_default()
    {
        FullQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance);

        PagedResult<TestProduct> result = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { PageSize = 0 },
            TestContext.Current.CancellationToken);

        result.Items.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_invalid_filter_key_is_ignored()
    {
        FullQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance);

        // "nodot" has no dot separator, should be ignored
        PagedResult<TestProduct> result = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest
            {
                Filter = new Dictionary<string, string>
                {
                    ["nodot"] = "value",
                    [".leadingdot"] = "value",
                    ["trailingdot."] = "value",
                },
            },
            TestContext.Current.CancellationToken);

        // No valid filters applied, returns all (with default quick filter)
        result.Items.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_invalid_operator_in_filter_is_ignored()
    {
        FullQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance);

        PagedResult<TestProduct> result = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest
            {
                Filter = new Dictionary<string, string>
                {
                    ["Name.invalidop"] = "value",
                },
            },
            TestContext.Current.CancellationToken);

        result.Items.ShouldNotBeEmpty();
    }

    [Fact]
    public void GetMetadata_includes_date_filters()
    {
        FullQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance);

        QueryMetadata metadata = engine.GetMetadata();

        metadata.DateFilters.Count.ShouldBe(1);
        metadata.DateFilters[0].Name.ShouldBe("CreatedAt");
        metadata.DateFilters[0].DefaultPeriod.ShouldBe(DatePeriod.ThisMonth);
        metadata.DateFilters[0].AvailablePeriods.Count.ShouldBe(7);
    }

    [Fact]
    public void GetMetadata_includes_invisible_columns()
    {
        FullQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance);

        QueryMetadata metadata = engine.GetMetadata();

        ColumnDefinition activeCol = metadata.Columns.First(c => c.Name == "IsActive");
        activeCol.IsVisible.ShouldBeFalse();
    }

    [Fact]
    public void GetMetadata_uses_property_name_when_label_is_null()
    {
        SimpleDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance);

        QueryMetadata metadata = engine.GetMetadata();

        metadata.Columns[0].Label.ShouldBe("Name");
    }

    [Fact]
    public void GetMetadata_supports_cursor_when_configured()
    {
        FullQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance);

        QueryMetadata metadata = engine.GetMetadata();

        metadata.Pagination.SupportsCursor.ShouldBeTrue();
    }

    [Fact]
    public void GetMetadata_filter_group_preset_uses_name_when_label_null()
    {
        PresetWithoutLabelDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance);

        QueryMetadata metadata = engine.GetMetadata();

        metadata.PresetFilterGroups[0].Label.ShouldBe("Status");
        metadata.PresetFilterGroups[0].Presets[0].Label.ShouldBe("Active");
    }

    [Fact]
    public void GetMetadata_quick_filter_uses_name_when_label_null()
    {
        FullQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance);

        QueryMetadata metadata = engine.GetMetadata();

        metadata.QuickFilters[0].Label.ShouldBe("Active");
    }

    private sealed class SimpleDefinition : QueryDefinition<TestProduct>
    {
        public override string Name => "Test.Simple";

        protected override void Configure(QueryDefinitionBuilder<TestProduct> builder) =>
            builder.Column(p => p.Name, c => c.Sortable());
    }

    private sealed class PresetWithoutLabelDefinition : QueryDefinition<TestProduct>
    {
        public override string Name => "Test.NoLabel";

        protected override void Configure(QueryDefinitionBuilder<TestProduct> builder) =>
            builder
                .Column(p => p.Name, c => c.Sortable())
                .FilterGroup("Status", g => g.Preset("Active", p => p.IsActive));
    }
}
