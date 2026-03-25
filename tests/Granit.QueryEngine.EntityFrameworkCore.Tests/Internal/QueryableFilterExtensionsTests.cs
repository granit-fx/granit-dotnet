using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

public sealed class ApplyFiltersTests
{
    [Fact]
    public void Applies_whitelisted_filter()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder.Column(p => p.Name, c => c.Filterable());

        List<TestProduct> source =
        [
            new() { Name = "Alice" },
            new() { Name = "Bob" },
        ];

        List<FilterCriteria> criteria = [new("Name", FilterOperator.Eq, "Alice")];

        var result = source.AsQueryable()
            .ApplyFilters(criteria, builder)
            .ToList();

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Alice");
    }

    [Fact]
    public void Ignores_non_whitelisted_filter()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder.Column(p => p.Name, c => c.Filterable());
        // Price is NOT declared as filterable

        List<TestProduct> source = [new() { Name = "A", Price = 100 }];
        List<FilterCriteria> criteria = [new("Price", FilterOperator.Gt, "50")];

        var result = source.AsQueryable()
            .ApplyFilters(criteria, builder)
            .ToList();

        result.Count.ShouldBe(1); // Filter ignored, all items returned
    }

    [Fact]
    public void Multiple_filters_applied_with_AND()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder
            .Column(p => p.Name, c => c.Filterable())
            .Column(p => p.Price, c => c.Filterable());

        List<TestProduct> source =
        [
            new() { Name = "Widget", Price = 100 },
            new() { Name = "Widget", Price = 50 },
            new() { Name = "Gadget", Price = 100 },
        ];

        List<FilterCriteria> criteria =
        [
            new("Name", FilterOperator.Eq, "Widget"),
            new("Price", FilterOperator.Gte, "100"),
        ];

        var result = source.AsQueryable()
            .ApplyFilters(criteria, builder)
            .ToList();

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Widget");
        result[0].Price.ShouldBe(100);
    }

    [Fact]
    public void Empty_criteria_returns_all()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        List<TestProduct> source = [new() { Name = "A" }, new() { Name = "B" }];

        var result = source.AsQueryable()
            .ApplyFilters([], builder)
            .ToList();

        result.Count.ShouldBe(2);
    }
}

public sealed class ApplyGlobalSearchTests
{
    [Fact]
    public void Searches_across_declared_properties()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder.GlobalSearch(p => p.Name);

        List<TestProduct> source =
        [
            new() { Name = "Alice Widget" },
            new() { Name = "Bob Gadget" },
        ];

        var result = source.AsQueryable()
            .ApplyGlobalSearch("Widget", builder)
            .ToList();

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Alice Widget");
    }

    [Fact]
    public void Empty_search_returns_all()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder.GlobalSearch(p => p.Name);

        List<TestProduct> source = [new() { Name = "A" }, new() { Name = "B" }];

        var result = source.AsQueryable()
            .ApplyGlobalSearch("", builder)
            .ToList();

        result.Count.ShouldBe(2);
    }

    [Fact]
    public void Null_search_returns_all()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder.GlobalSearch(p => p.Name);

        List<TestProduct> source = [new() { Name = "A" }];

        var result = source.AsQueryable()
            .ApplyGlobalSearch(null!, builder)
            .ToList();

        result.Count.ShouldBe(1);
    }

    [Fact]
    public void No_global_search_properties_returns_all()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();

        List<TestProduct> source = [new() { Name = "A" }];

        var result = source.AsQueryable()
            .ApplyGlobalSearch("A", builder)
            .ToList();

        result.Count.ShouldBe(1);
    }
}

public sealed class ApplyPresetsTests
{
    [Fact]
    public void Applies_default_presets_when_none_specified()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder.FilterGroup("Status", g => g
            .Preset("Expensive", p => p.Price >= 100, isDefault: true)
            .Preset("Cheap", p => p.Price < 100));

        List<TestProduct> source =
        [
            new() { Name = "A", Price = 150 },
            new() { Name = "B", Price = 50 },
        ];

        var result = source.AsQueryable()
            .ApplyPresets(null, builder)
            .ToList();

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("A");
    }

    [Fact]
    public void Applies_specified_preset()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder.FilterGroup("Status", g => g
            .Preset("Expensive", p => p.Price >= 100, isDefault: true)
            .Preset("Cheap", p => p.Price < 100));

        List<TestProduct> source =
        [
            new() { Name = "A", Price = 150 },
            new() { Name = "B", Price = 50 },
        ];

        Dictionary<string, string> presets = new() { ["Status"] = "Cheap" };

        var result = source.AsQueryable()
            .ApplyPresets(presets, builder)
            .ToList();

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("B");
    }

    [Fact]
    public void OR_within_group_multiple_presets()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder.FilterGroup("Category", g => g
            .Preset("Electronics", p => p.Category == ProductCategory.Electronics)
            .Preset("Books", p => p.Category == ProductCategory.Books)
            .Preset("Clothing", p => p.Category == ProductCategory.Clothing));

        List<TestProduct> source =
        [
            new() { Name = "Laptop", Category = ProductCategory.Electronics },
            new() { Name = "Novel", Category = ProductCategory.Books },
            new() { Name = "Shirt", Category = ProductCategory.Clothing },
        ];

        Dictionary<string, string> presets = new() { ["Category"] = "Electronics,Books" };

        var result = source.AsQueryable()
            .ApplyPresets(presets, builder)
            .ToList();

        result.Count.ShouldBe(2);
    }

    [Fact]
    public void AND_between_groups()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder
            .FilterGroup("Category", g => g
                .Preset("Electronics", p => p.Category == ProductCategory.Electronics))
            .FilterGroup("PriceRange", g => g
                .Preset("Expensive", p => p.Price >= 100));

        List<TestProduct> source =
        [
            new() { Name = "Laptop", Category = ProductCategory.Electronics, Price = 1000 },
            new() { Name = "Cable", Category = ProductCategory.Electronics, Price = 10 },
            new() { Name = "Novel", Category = ProductCategory.Books, Price = 200 },
        ];

        Dictionary<string, string> presets = new()
        {
            ["Category"] = "Electronics",
            ["PriceRange"] = "Expensive",
        };

        var result = source.AsQueryable()
            .ApplyPresets(presets, builder)
            .ToList();

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Laptop");
    }

    [Fact]
    public void No_filter_groups_returns_all()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        List<TestProduct> source = [new() { Name = "A" }];

        var result = source.AsQueryable()
            .ApplyPresets(null, builder)
            .ToList();

        result.Count.ShouldBe(1);
    }
}
