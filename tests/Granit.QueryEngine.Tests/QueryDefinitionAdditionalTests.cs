using Granit.QueryEngine.Filtering;
using Granit.QueryEngine.Options;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Tests;

public sealed class QueryDefinitionAdditionalTests
{
    [Fact]
    public void Initialize_applies_options_to_builder()
    {
        FullQueryDefinition definition = new();
        QueryEngineOptions options = new()
        {
            DefaultPageSize = 50,
            MaxPageSize = 500,
            MaxStreamSize = 200_000,
        };

        definition.Initialize(options);

        // GetDefaultPageSize returns builder-overridden value (25), not options (50)
        definition.GetDefaultPageSize().ShouldBe(25);
    }

    [Fact]
    public void Initialize_options_used_when_builder_does_not_override()
    {
        MinimalQueryDefinition definition = new();
        QueryEngineOptions options = new()
        {
            DefaultPageSize = 50,
            MaxPageSize = 500,
        };

        definition.Initialize(options);

        definition.GetDefaultPageSize().ShouldBe(50);
        definition.GetMaxPageSize().ShouldBe(500);
    }

    [Fact]
    public void GetQuickFilters_returns_declared_filters()
    {
        FullQueryDefinition definition = new();

        IReadOnlyList<QuickFilterDescriptor> filters = definition.GetQuickFilters();

        filters.Count.ShouldBe(1);
        filters[0].Name.ShouldBe("Active");
    }

    [Fact]
    public void GetFilterGroups_returns_empty_when_none_declared()
    {
        MinimalQueryDefinition definition = new();

        IReadOnlyList<FilterGroupDescriptor> groups = definition.GetFilterGroups();

        groups.ShouldBeEmpty();
    }

    [Fact]
    public void GetDateFilters_returns_empty_when_none_declared()
    {
        MinimalQueryDefinition definition = new();

        IReadOnlyList<DateFilterDescriptor> filters = definition.GetDateFilters();

        filters.ShouldBeEmpty();
    }

    [Fact]
    public void GetGroupByFields_returns_empty_when_none_declared()
    {
        MinimalQueryDefinition definition = new();

        IReadOnlyList<GroupByDescriptor> fields = definition.GetGroupByFields();

        fields.ShouldBeEmpty();
    }

    [Fact]
    public void GetAggregates_returns_empty_when_none_declared()
    {
        MinimalQueryDefinition definition = new();

        IReadOnlyList<AggregateDescriptor> aggregates = definition.GetAggregates();

        aggregates.ShouldBeEmpty();
    }

    [Fact]
    public void GetGlobalSearchProperties_returns_empty_when_none_declared()
    {
        MinimalQueryDefinition definition = new();

        IReadOnlyList<string> properties = definition.GetGlobalSearchProperties();

        properties.ShouldBeEmpty();
    }

    [Fact]
    public void GetCursorProperty_returns_null_when_not_configured()
    {
        MinimalQueryDefinition definition = new();

        definition.GetCursorProperty().ShouldBeNull();
    }

    [Fact]
    public void GetDefaultSort_returns_null_when_not_configured()
    {
        MinimalQueryDefinition definition = new();

        definition.GetDefaultSort().ShouldBeNull();
    }

    private sealed class FullQueryDefinition : QueryDefinition<TestEntity>
    {
        public override string Name => "Test.Full";

        protected override void Configure(QueryDefinitionBuilder<TestEntity> builder) =>
            builder
                .Column(e => e.Name, c => c.Label("Name").Sortable().Filterable())
                .QuickFilter("Active", e => e.Age > 0, isDefault: true)
                .DefaultPageSize(25)
                .MaxPageSize(200);
    }

    private sealed class MinimalQueryDefinition : QueryDefinition<TestEntity>
    {
        public override string Name => "Test.Minimal";

        protected override void Configure(QueryDefinitionBuilder<TestEntity> builder) =>
            builder.Column(e => e.Name);
    }
}
