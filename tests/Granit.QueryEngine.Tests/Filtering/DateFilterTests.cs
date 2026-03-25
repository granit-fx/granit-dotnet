using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Tests.Filtering;

public sealed class DateFilterTests
{
    [Fact]
    public void DateFilter_registers_with_default_period()
    {
        QueryDefinitionBuilder<DateTestEntity> builder = new();

        builder.DateFilter(e => e.CreatedAt);

        builder.DateFilters.Count.ShouldBe(1);
        builder.DateFilters[0].PropertyName.ShouldBe("CreatedAt");
        builder.DateFilters[0].ClrType.ShouldBe(typeof(DateTimeOffset));
        builder.DateFilters[0].DefaultPeriod.ShouldBe(DatePeriod.ThisMonth);
    }

    [Fact]
    public void DateFilter_with_custom_default_period()
    {
        QueryDefinitionBuilder<DateTestEntity> builder = new();

        builder.DateFilter(e => e.CreatedAt, DatePeriod.ThisYear);

        builder.DateFilters[0].DefaultPeriod.ShouldBe(DatePeriod.ThisYear);
    }

    [Fact]
    public void DateFilter_supports_DateOnly()
    {
        QueryDefinitionBuilder<DateTestEntity> builder = new();

        builder.DateFilter(e => e.BirthDate);

        builder.DateFilters[0].ClrType.ShouldBe(typeof(DateOnly?));
    }

    [Fact]
    public void Multiple_date_filters()
    {
        QueryDefinitionBuilder<DateTestEntity> builder = new();

        builder
            .DateFilter(e => e.CreatedAt, DatePeriod.ThisMonth)
            .DateFilter(e => e.ModifiedAt, DatePeriod.ThisWeek);

        builder.DateFilters.Count.ShouldBe(2);
    }

    [Fact]
    public void QueryDefinition_exposes_date_filters()
    {
        TestDateDefinition definition = new();

        IReadOnlyList<DateFilterDescriptor> filters = definition.GetDateFilters();

        filters.Count.ShouldBe(1);
        filters[0].PropertyName.ShouldBe("CreatedAt");
        filters[0].DefaultPeriod.ShouldBe(DatePeriod.ThisQuarter);
    }

    private sealed class TestDateDefinition : QueryDefinition<DateTestEntity>
    {
        public override string Name => "Test.DateFilters";

        protected override void Configure(QueryDefinitionBuilder<DateTestEntity> builder) =>
            builder.DateFilter(e => e.CreatedAt, DatePeriod.ThisQuarter);
    }
}

public sealed class GroupByTests
{
    [Fact]
    public void AllowGroupBy_registers_field()
    {
        QueryDefinitionBuilder<DateTestEntity> builder = new();

        builder.AllowGroupBy(e => e.Status);

        builder.GroupByFields.Count.ShouldBe(1);
        builder.GroupByFields[0].PropertyName.ShouldBe("Status");
        builder.GroupByFields[0].ClrType.ShouldBe(typeof(string));
    }

    [Fact]
    public void Multiple_group_by_fields()
    {
        QueryDefinitionBuilder<DateTestEntity> builder = new();

        builder
            .AllowGroupBy(e => e.Status)
            .AllowGroupBy(e => e.CreatedAt);

        builder.GroupByFields.Count.ShouldBe(2);
    }

    [Fact]
    public void QueryDefinition_exposes_group_by_fields()
    {
        TestGroupByDefinition definition = new();

        IReadOnlyList<Granit.QueryEngine.Filtering.GroupByDescriptor> fields = definition.GetGroupByFields();

        fields.Count.ShouldBe(1);
        fields[0].PropertyName.ShouldBe("Status");
    }

    private sealed class TestGroupByDefinition : QueryDefinition<DateTestEntity>
    {
        public override string Name => "Test.GroupBy";

        protected override void Configure(QueryDefinitionBuilder<DateTestEntity> builder) =>
            builder.AllowGroupBy(e => e.Status);
    }
}

public sealed class AggregateTests
{
    [Fact]
    public void Aggregate_registers_with_function_and_alias()
    {
        QueryDefinitionBuilder<DateTestEntity> builder = new();

        builder.Aggregate(e => e.Amount, AggregateFunction.Sum, "totalAmount");

        builder.Aggregates.Count.ShouldBe(1);
        builder.Aggregates[0].PropertyName.ShouldBe("Amount");
        builder.Aggregates[0].ClrType.ShouldBe(typeof(decimal));
        builder.Aggregates[0].Function.ShouldBe(AggregateFunction.Sum);
        builder.Aggregates[0].Alias.ShouldBe("totalAmount");
    }

    [Fact]
    public void Multiple_aggregates()
    {
        QueryDefinitionBuilder<DateTestEntity> builder = new();

        builder
            .Aggregate(e => e.Amount, AggregateFunction.Sum, "totalAmount")
            .Aggregate(e => e.Amount, AggregateFunction.Avg, "avgAmount")
            .Aggregate(e => e.Amount, AggregateFunction.Max, "maxAmount");

        builder.Aggregates.Count.ShouldBe(3);
    }

    [Fact]
    public void QueryDefinition_exposes_aggregates()
    {
        TestAggregateDefinition definition = new();

        IReadOnlyList<Granit.QueryEngine.Filtering.AggregateDescriptor> aggregates = definition.GetAggregates();

        aggregates.Count.ShouldBe(1);
        aggregates[0].Alias.ShouldBe("totalAmount");
    }

    private sealed class TestAggregateDefinition : QueryDefinition<DateTestEntity>
    {
        public override string Name => "Test.Aggregates";

        protected override void Configure(QueryDefinitionBuilder<DateTestEntity> builder) =>
            builder.Aggregate(e => e.Amount, AggregateFunction.Sum, "totalAmount");
    }
}

public sealed class DateTestEntity
{
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ModifiedAt { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
