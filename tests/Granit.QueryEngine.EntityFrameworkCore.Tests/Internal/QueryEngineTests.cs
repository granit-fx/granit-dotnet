using Granit.DataLookup.Descriptors;
using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Granit.QueryEngine.Filtering;
using Granit.QueryEngine.Meta;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

public sealed class QueryEngineTests : IAsyncLifetime
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
                .AllowGroupBy(p => p.Category)
                .Aggregate(p => p.Price, AggregateFunction.Sum, "totalPrice")
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

    [Fact]
    public async Task ExecuteAsync_returns_paged_result()
    {
        ProductQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        PagedResult<TestProduct> result = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest(),
            TestContext.Current.CancellationToken);

        // Default preset filters to Electronics only (3 items)
        result.TotalCount.ShouldBe(3);
        result.Items.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ExecuteAsync_applies_filter()
    {
        ProductQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        PagedResult<TestProduct> result = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest
            {
                Filter = new Dictionary<string, string> { ["Price.gte"] = "500" },
                Presets = new Dictionary<string, string> { ["Category"] = "Electronics" },
            },
            TestContext.Current.CancellationToken);

        result.Items.ShouldAllBe(p => p.Price >= 500);
    }

    [Fact]
    public async Task ExecuteAsync_applies_search()
    {
        ProductQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        PagedResult<TestProduct> result = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest
            {
                Search = "Laptop",
                Presets = new Dictionary<string, string> { ["Category"] = "Electronics" },
            },
            TestContext.Current.CancellationToken);

        result.Items.Count.ShouldBe(1);
        result.Items[0].Name.ShouldBe("Laptop");
    }

    [Fact]
    public async Task ExecuteAsync_applies_sort()
    {
        ProductQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        PagedResult<TestProduct> result = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest
            {
                Sort = "Price",
                Presets = new Dictionary<string, string> { ["Category"] = "Electronics" },
            },
            TestContext.Current.CancellationToken);

        result.Items[0].Price.ShouldBeLessThanOrEqualTo(result.Items[1].Price);
    }

    [Fact]
    public async Task ExecuteAsync_clamps_page_size()
    {
        ProductQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        PagedResult<TestProduct> result = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { PageSize = 1000 },
            TestContext.Current.CancellationToken);

        result.Items.Count.ShouldBeLessThanOrEqualTo(50);
    }

    [Fact]
    public async Task ExecuteAsync_pages_correctly()
    {
        ProductQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        PagedResult<TestProduct> page1 = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest
            {
                Page = 1,
                PageSize = 2,
                Presets = new Dictionary<string, string> { ["Category"] = "Electronics" },
            },
            TestContext.Current.CancellationToken);

        PagedResult<TestProduct> page2 = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest
            {
                Page = 2,
                PageSize = 2,
                Presets = new Dictionary<string, string> { ["Category"] = "Electronics" },
            },
            TestContext.Current.CancellationToken);

        page1.Items.Count.ShouldBe(2);
        page2.Items.Count.ShouldBe(1);
        page1.TotalCount.ShouldBe(3);
    }

    [Fact]
    public void GetMetadata_returns_complete_metadata()
    {
        ProductQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        QueryMetadata metadata = engine.GetMetadata();

        metadata.Columns.Count.ShouldBe(3);
        metadata.FilterableFields.Count.ShouldBe(3);
        metadata.SortableFields.Count.ShouldBe(2);
        metadata.PresetFilterGroups.Count.ShouldBe(1);
        metadata.GroupByFields.Count.ShouldBe(1);
        metadata.Pagination.DefaultPageSize.ShouldBe(10);
        metadata.Pagination.MaxPageSize.ShouldBe(50);
        metadata.Pagination.SupportsCursor.ShouldBeFalse();
        metadata.DefaultSort.ShouldBe("-Price");
    }

    [Fact]
    public async Task ExecuteAsync_applies_quick_filter()
    {
        QuickFilterDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        // Explicitly activate "Expensive" quick filter
        PagedResult<TestProduct> result = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { QuickFilters = ["Expensive"] },
            TestContext.Current.CancellationToken);

        result.Items.ShouldAllBe(p => p.Price >= 500);
    }

    [Fact]
    public async Task ExecuteAsync_applies_default_quick_filters_when_none_specified()
    {
        QuickFilterDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        // No quick filters specified → default "Active" filter applied
        PagedResult<TestProduct> result = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest(),
            TestContext.Current.CancellationToken);

        result.Items.ShouldAllBe(p => p.Activated);
        result.TotalCount.ShouldBe(5); // All test products are active
    }

    [Fact]
    public async Task ExecuteAsync_combines_quick_filters_with_AND()
    {
        QuickFilterDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        // Activate both "Expensive" and "Active" → AND semantics
        PagedResult<TestProduct> result = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { QuickFilters = ["Expensive", "Active"] },
            TestContext.Current.CancellationToken);

        result.Items.ShouldAllBe(p => p.Price >= 500 && p.Activated);
    }

    [Fact]
    public void GetMetadata_includes_quick_filters()
    {
        QuickFilterDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        QueryMetadata metadata = engine.GetMetadata();

        metadata.QuickFilters.Count.ShouldBe(2);
        metadata.QuickFilters[0].Name.ShouldBe("Active");
        metadata.QuickFilters[0].Label.ShouldBe("Actifs uniquement");
        metadata.QuickFilters[0].IsDefault.ShouldBeTrue();
        metadata.QuickFilters[1].Name.ShouldBe("Expensive");
        metadata.QuickFilters[1].Label.ShouldBe("Expensive");
        metadata.QuickFilters[1].IsDefault.ShouldBeFalse();
    }

    [Fact]
    public void GetMetadata_includes_filter_operators_for_fields()
    {
        ProductQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        QueryMetadata metadata = engine.GetMetadata();

        FilterableField nameField = metadata.FilterableFields
            .First(f => f.Name == "Name");
        nameField.Operators.ShouldContain(FilterOperator.Contains);
        nameField.Operators.ShouldContain(FilterOperator.Eq);

        FilterableField priceField = metadata.FilterableFields
            .First(f => f.Name == "Price");
        priceField.Operators.ShouldContain(FilterOperator.Gt);
        priceField.Operators.ShouldContain(FilterOperator.Between);
    }

    [Fact]
    public void GetMetadata_populates_enum_values_for_enum_fields()
    {
        ProductQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        QueryMetadata metadata = engine.GetMetadata();

        FilterableField categoryField = metadata.FilterableFields
            .First(f => f.Name == "Category");
        categoryField.EnumValues.ShouldNotBeNull();
        categoryField.EnumValues.ShouldBe(["Electronics", "Books", "Clothing"], ignoreOrder: true);

        FilterableField nameField = metadata.FilterableFields
            .First(f => f.Name == "Name");
        nameField.EnumValues.ShouldBeNull();

        FilterableField priceField = metadata.FilterableFields
            .First(f => f.Name == "Price");
        priceField.EnumValues.ShouldBeNull();
    }

    [Fact]
    public void GetMetadata_emits_lookup_descriptor_when_declared()
    {
        LookupDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        QueryMetadata metadata = engine.GetMetadata();

        FilterableField tenantField = metadata.FilterableFields
            .First(f => f.Name == "Name");
        tenantField.Lookup.ShouldNotBeNull();
        tenantField.Lookup!.Name.ShouldBe("tenants");
        tenantField.Lookup.Kind.ShouldBe(LookupKind.QueryEngine);
        tenantField.Lookup.RequiredPermission.ShouldBe("Platform.Tenants.Read");
    }

    [Fact]
    public void GetMetadata_lookup_is_null_when_not_declared()
    {
        ProductQueryDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        QueryMetadata metadata = engine.GetMetadata();

        foreach (FilterableField field in metadata.FilterableFields)
        {
            field.Lookup.ShouldBeNull();
        }
    }

    [Fact]
    public void GetMetadata_emits_lookup_with_scope_keys_and_endpoint_override()
    {
        CustomEndpointLookupDefinition definition = new();
        QueryEngine<TestProduct> engine = new(definition, NullLogger<QueryEngine<TestProduct>>.Instance, Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        QueryMetadata metadata = engine.GetMetadata();

        FilterableField field = metadata.FilterableFields.First(f => f.Name == "Name");
        field.Lookup.ShouldNotBeNull();
        field.Lookup!.Name.ShouldBeNull();
        field.Lookup.Endpoint.ShouldBe("/api/external/stripe/customers");
        field.Lookup.ScopeKeys.ShouldBe(["tenantId"]);
    }

    private sealed class LookupDefinition : QueryDefinition<TestProduct>
    {
        public override string Name => "Test.Products.Lookup";

        protected override void Configure(QueryDefinitionBuilder<TestProduct> builder) =>
            builder
                .Column(p => p.Name, c => c
                    .Label("Name")
                    .Filterable()
                    .Lookup("tenants", requiredPermission: "Platform.Tenants.Read"))
                .Column(p => p.Price, c => c.Label("Price").Filterable())
                .DefaultPageSize(10);
    }

    private sealed class CustomEndpointLookupDefinition : QueryDefinition<TestProduct>
    {
        public override string Name => "Test.Products.CustomLookup";

        protected override void Configure(QueryDefinitionBuilder<TestProduct> builder) =>
            builder
                .Column(p => p.Name, c => c
                    .Label("Name")
                    .Filterable()
                    .Lookup(new LookupDescriptor(
                        Endpoint: "/api/external/stripe/customers",
                        Kind: LookupKind.Simple,
                        ScopeKeys: ["tenantId"])))
                .DefaultPageSize(10);
    }

    private sealed class QuickFilterDefinition : QueryDefinition<TestProduct>
    {
        public override string Name => "Test.Products.QuickFilter";

        protected override void Configure(QueryDefinitionBuilder<TestProduct> builder) =>
            builder
                .Column(p => p.Name, c => c.Label("Name").Sortable().Filterable())
                .Column(p => p.Price, c => c.Label("Price").Sortable().Filterable())
                .QuickFilter("Active", "Actifs uniquement", p => p.Activated, isDefault: true)
                .QuickFilter("Expensive", p => p.Price >= 500)
                .DefaultPageSize(10)
                .MaxPageSize(50);
    }
}
