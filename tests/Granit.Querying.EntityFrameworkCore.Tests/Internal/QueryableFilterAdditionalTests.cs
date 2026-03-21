using Granit.Querying.EntityFrameworkCore.Internal;
using Shouldly;
using Xunit;

namespace Granit.Querying.EntityFrameworkCore.Tests.Internal;

public sealed class ApplyQuickFiltersTests
{
    [Fact]
    public void ApplyQuickFilters_no_quick_filters_defined_returns_all()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        // No quick filters registered

        List<TestProduct> source = [new() { Name = "A" }, new() { Name = "B" }];

        var result = source.AsQueryable()
            .ApplyQuickFilters(null, builder)
            .ToList();

        result.Count.ShouldBe(2);
    }

    [Fact]
    public void ApplyQuickFilters_applies_default_when_none_specified()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder.QuickFilter("Active", p => p.IsActive, isDefault: true);
        builder.QuickFilter("Expensive", p => p.Price >= 500);

        List<TestProduct> source =
        [
            new() { Name = "A", IsActive = true, Price = 100 },
            new() { Name = "B", IsActive = false, Price = 100 },
        ];

        var result = source.AsQueryable()
            .ApplyQuickFilters(null, builder)
            .ToList();

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("A");
    }

    [Fact]
    public void ApplyQuickFilters_applies_empty_list_uses_defaults()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder.QuickFilter("Active", p => p.IsActive, isDefault: true);

        List<TestProduct> source =
        [
            new() { Name = "A", IsActive = true },
            new() { Name = "B", IsActive = false },
        ];

        var result = source.AsQueryable()
            .ApplyQuickFilters([], builder)
            .ToList();

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("A");
    }

    [Fact]
    public void ApplyQuickFilters_applies_explicit_filter()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder.QuickFilter("Active", p => p.IsActive, isDefault: true);
        builder.QuickFilter("Expensive", p => p.Price >= 500);

        List<TestProduct> source =
        [
            new() { Name = "A", IsActive = true, Price = 100 },
            new() { Name = "B", IsActive = true, Price = 600 },
        ];

        var result = source.AsQueryable()
            .ApplyQuickFilters(["Expensive"], builder)
            .ToList();

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("B");
    }

    [Fact]
    public void ApplyQuickFilters_AND_semantics_for_multiple()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder.QuickFilter("Active", p => p.IsActive);
        builder.QuickFilter("Expensive", p => p.Price >= 500);

        List<TestProduct> source =
        [
            new() { Name = "A", IsActive = true, Price = 100 },
            new() { Name = "B", IsActive = true, Price = 600 },
            new() { Name = "C", IsActive = false, Price = 600 },
        ];

        var result = source.AsQueryable()
            .ApplyQuickFilters(["Active", "Expensive"], builder)
            .ToList();

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("B");
    }

    [Fact]
    public void ApplyQuickFilters_case_insensitive_name_matching()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder.QuickFilter("Active", p => p.IsActive);

        List<TestProduct> source =
        [
            new() { Name = "A", IsActive = true },
            new() { Name = "B", IsActive = false },
        ];

        var result = source.AsQueryable()
            .ApplyQuickFilters(["active"], builder)
            .ToList();

        result.Count.ShouldBe(1);
    }

    [Fact]
    public void ApplyQuickFilters_unknown_filter_name_is_ignored()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder.QuickFilter("Active", p => p.IsActive);

        List<TestProduct> source =
        [
            new() { Name = "A", IsActive = true },
            new() { Name = "B", IsActive = false },
        ];

        var result = source.AsQueryable()
            .ApplyQuickFilters(["NonExistent"], builder)
            .ToList();

        // Unknown filter ignored — returns all
        result.Count.ShouldBe(2);
    }
}

public sealed class ApplyPresetsAdditionalTests
{
    [Fact]
    public void ApplyPresets_empty_presets_dict_applies_defaults()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder.FilterGroup("Status", g => g
            .Preset("Active", p => p.IsActive, isDefault: true)
            .Preset("Inactive", p => !p.IsActive));

        List<TestProduct> source =
        [
            new() { Name = "A", IsActive = true },
            new() { Name = "B", IsActive = false },
        ];

        var result = source.AsQueryable()
            .ApplyPresets(new Dictionary<string, string>(), builder)
            .ToList();

        // Empty dict = no explicit presets, but has entries → applies default presets
        // Actually the code checks activePresets.Count == 0 → applies defaults
        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("A");
    }

    [Fact]
    public void ApplyPresets_group_without_defaults_returns_all()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder.FilterGroup("Status", g => g
            .Preset("Active", p => p.IsActive)
            .Preset("Inactive", p => !p.IsActive));

        List<TestProduct> source =
        [
            new() { Name = "A", IsActive = true },
            new() { Name = "B", IsActive = false },
        ];

        // No defaults set, no explicit presets
        var result = source.AsQueryable()
            .ApplyPresets(null, builder)
            .ToList();

        result.Count.ShouldBe(2);
    }

    [Fact]
    public void ApplyPresets_unmatched_group_name_is_ignored()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder.FilterGroup("Status", g => g
            .Preset("Active", p => p.IsActive));

        List<TestProduct> source = [new() { Name = "A", IsActive = true }];

        Dictionary<string, string> presets = new() { ["NonExistentGroup"] = "Active" };

        var result = source.AsQueryable()
            .ApplyPresets(presets, builder)
            .ToList();

        result.Count.ShouldBe(1);
    }

    [Fact]
    public void ApplyPresets_unmatched_preset_name_is_ignored()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder.FilterGroup("Status", g => g
            .Preset("Active", p => p.IsActive));

        List<TestProduct> source =
        [
            new() { Name = "A", IsActive = true },
            new() { Name = "B", IsActive = false },
        ];

        Dictionary<string, string> presets = new() { ["Status"] = "NonExistent" };

        var result = source.AsQueryable()
            .ApplyPresets(presets, builder)
            .ToList();

        // No matching preset found — returns all (BuildGroupPredicate returns null)
        result.Count.ShouldBe(2);
    }
}
