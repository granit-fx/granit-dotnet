using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Tests.Metrics;

public sealed class MetricDefinitionTests
{
    [Fact]
    public void Defaults_AreReasonable()
    {
        SampleCountMetricDefinition metric = new();

        metric.Name.ShouldBe("Sample.Count");
        metric.ValueKind.ShouldBe(MetricValueKind.Count);
        metric.Aggregation.ShouldBe(AggregateFunction.Count);
        metric.IsHigherBetter.ShouldBeTrue();
        metric.RefreshHint.ShouldBe(RefreshHint.Dynamic);
        metric.Selector.ShouldBeNull();
        metric.CurrencyCode.ShouldBeNull();
    }

    [Fact]
    public void Implements_IMetricDefinitionDescriptor()
    {
        SampleCountMetricDefinition metric = new();

        metric.ShouldBeAssignableTo<IMetricDefinitionDescriptor>();
        IMetricDefinitionDescriptor descriptor = metric;
        descriptor.EntityType.ShouldBe(typeof(SampleEntity));
        descriptor.ValueType.ShouldBe(typeof(int));
    }

    [Fact]
    public void SumMetric_ExposesNullableSelector()
    {
        SampleAmountSumMetricDefinition metric = new();

        metric.Aggregation.ShouldBe(AggregateFunction.Sum);
        metric.Selector.ShouldNotBeNull();
        metric.ValueKind.ShouldBe(MetricValueKind.Currency);
        metric.CurrencyCode.ShouldBe("EUR");
        metric.IsHigherBetter.ShouldBeFalse();
    }

    private sealed class SampleEntity
    {
        public int Id { get; init; }
        public decimal Amount { get; init; }
    }

    private sealed class SampleCountMetricDefinition : MetricDefinition<SampleEntity, int>
    {
        public override string Name => "Sample.Count";
        public override MetricValueKind ValueKind => MetricValueKind.Count;
        public override AggregateFunction Aggregation => AggregateFunction.Count;
        public override Expression<Func<SampleEntity, int?>>? Selector => null;
    }

    private sealed class SampleAmountSumMetricDefinition : MetricDefinition<SampleEntity, decimal>
    {
        public override string Name => "Sample.AmountSum";
        public override MetricValueKind ValueKind => MetricValueKind.Currency;
        public override AggregateFunction Aggregation => AggregateFunction.Sum;
        public override Expression<Func<SampleEntity, decimal?>>? Selector => e => e.Amount;
        public override string? CurrencyCode => "EUR";
        public override bool IsHigherBetter => false;
    }
}
