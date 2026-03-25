using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Tests.Filtering;

public sealed class FilterGroupBuilderTests
{
    [Fact]
    public void Preset_registers_with_name_and_predicate()
    {
        FilterGroupBuilder<TestEntity> builder = new();

        builder.Preset("Active", e => e.Age > 0);

        builder.Presets.Count.ShouldBe(1);
        builder.Presets[0].Name.ShouldBe("Active");
        builder.Presets[0].Label.ShouldBeNull();
        builder.Presets[0].IsDefault.ShouldBeFalse();
        builder.Presets[0].Predicate.ShouldNotBeNull();
    }

    [Fact]
    public void Preset_with_label_registers_label()
    {
        FilterGroupBuilder<TestEntity> builder = new();

        builder.Preset("Active", "Actif", e => e.Age > 0);

        builder.Presets[0].Name.ShouldBe("Active");
        builder.Presets[0].Label.ShouldBe("Actif");
    }

    [Fact]
    public void Preset_with_default_sets_flag()
    {
        FilterGroupBuilder<TestEntity> builder = new();

        builder.Preset("Active", e => e.Age > 0, isDefault: true);

        builder.Presets[0].IsDefault.ShouldBeTrue();
    }

    [Fact]
    public void Multiple_presets_are_registered_in_order()
    {
        FilterGroupBuilder<TestEntity> builder = new();

        builder
            .Preset("Active", "Actif", e => e.Age > 0, isDefault: true)
            .Preset("Inactive", "Inactif", e => e.Age == 0);

        builder.Presets.Count.ShouldBe(2);
        builder.Presets[0].Name.ShouldBe("Active");
        builder.Presets[1].Name.ShouldBe("Inactive");
    }

    [Fact]
    public void Fluent_chaining_returns_same_builder()
    {
        FilterGroupBuilder<TestEntity> builder = new();

        FilterGroupBuilder<TestEntity> result = builder
            .Preset("A", e => e.Age > 0)
            .Preset("B", e => e.Age == 0);

        result.ShouldBeSameAs(builder);
    }
}

public sealed class FilterGroupIntegrationTests
{
    [Fact]
    public void FilterGroup_on_QueryDefinitionBuilder_registers_group()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        builder.FilterGroup("Status", group => group
            .Preset("Active", "Actif", e => e.Age > 0, isDefault: true)
            .Preset("Inactive", "Inactif", e => e.Age == 0));

        builder.FilterGroups.Count.ShouldBe(1);
        builder.FilterGroups[0].Name.ShouldBe("Status");
        builder.FilterGroups[0].Label.ShouldBeNull();
        builder.FilterGroups[0].Presets.Count.ShouldBe(2);
    }

    [Fact]
    public void FilterGroup_with_label_on_QueryDefinitionBuilder()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        builder.FilterGroup("Status", "Statut", group => group
            .Preset("Active", e => e.Age > 0));

        builder.FilterGroups[0].Label.ShouldBe("Statut");
    }

    [Fact]
    public void Multiple_filter_groups_are_independent()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        builder
            .FilterGroup("Status", g => g.Preset("Active", e => e.Age > 0))
            .FilterGroup("Category", g => g.Preset("Premium", e => e.Name == "Premium"));

        builder.FilterGroups.Count.ShouldBe(2);
        builder.FilterGroups[0].Name.ShouldBe("Status");
        builder.FilterGroups[1].Name.ShouldBe("Category");
    }

    [Fact]
    public void QueryDefinition_exposes_filter_groups()
    {
        TestQueryDefinition definition = new();

        Granit.QueryEngine.Filtering.FilterGroupDescriptor group = definition.GetFilterGroups()[0];

        group.Name.ShouldBe("Status");
        group.Presets.Count.ShouldBe(2);
        group.Presets[0].Name.ShouldBe("Active");
        group.Presets[0].IsDefault.ShouldBeTrue();
    }

    private sealed class TestQueryDefinition : QueryDefinition<TestEntity>
    {
        public override string Name => "Test.Filtered";

        protected override void Configure(QueryDefinitionBuilder<TestEntity> builder) =>
            builder
                .Column(e => e.Name)
                .FilterGroup("Status", g => g
                    .Preset("Active", e => e.Age > 0, isDefault: true)
                    .Preset("Inactive", e => e.Age == 0));
    }
}
