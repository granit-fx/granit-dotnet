using Granit.AI.Endpoints.Queries;
using Shouldly;
using Xunit;

namespace Granit.AI.Endpoints.Tests.Queries;

public sealed class AIUsageRecordQueryDefinitionTests
{
    private readonly AIUsageRecordQueryDefinition _definition = new();

    [Fact]
    public void Name_is_AI_UsageRecords() =>
        _definition.Name.ShouldBe("AI.UsageRecords");

    [Fact]
    public void Declares_expected_columns()
    {
        System.Collections.Generic.IReadOnlyList<Granit.QueryEngine.ColumnDescriptor> columns = _definition.GetColumns();
        columns.Count.ShouldBe(9);
        columns.Select(c => c.PropertyName).ShouldContain("WorkspaceName");
        columns.Select(c => c.PropertyName).ShouldContain("Provider");
        columns.Select(c => c.PropertyName).ShouldContain("Model");
        columns.Select(c => c.PropertyName).ShouldContain("InputTokens");
        columns.Select(c => c.PropertyName).ShouldContain("OutputTokens");
        columns.Select(c => c.PropertyName).ShouldContain("EstimatedCost");
        columns.Select(c => c.PropertyName).ShouldContain("CostCurrency");
        columns.Select(c => c.PropertyName).ShouldContain("Timestamp");
        columns.Select(c => c.PropertyName).ShouldContain("Duration");
    }

    [Fact]
    public void Declares_groupby_fields()
    {
        System.Collections.Generic.IReadOnlyList<Granit.QueryEngine.Filtering.GroupByDescriptor> groupByFields = _definition.GetGroupByFields();
        groupByFields.Count.ShouldBe(3);
        groupByFields.Select(g => g.PropertyName).ShouldContain("WorkspaceName");
        groupByFields.Select(g => g.PropertyName).ShouldContain("Provider");
        groupByFields.Select(g => g.PropertyName).ShouldContain("Model");
    }

    [Fact]
    public void Declares_aggregates()
    {
        System.Collections.Generic.IReadOnlyList<Granit.QueryEngine.Filtering.AggregateDescriptor> aggregates = _definition.GetAggregates();
        aggregates.Count.ShouldBe(3);
        aggregates.Select(a => a.Alias).ShouldContain("totalInputTokens");
        aggregates.Select(a => a.Alias).ShouldContain("totalOutputTokens");
        aggregates.Select(a => a.Alias).ShouldContain("totalEstimatedCost");
    }

    [Fact]
    public void Default_sort_is_descending_timestamp() =>
        _definition.GetDefaultSort().ShouldBe("-timestamp");

    [Fact]
    public void Default_page_size_is_25() =>
        _definition.GetDefaultPageSize().ShouldBe(25);
}
