using System.Linq.Expressions;
using Granit.Analytics.Extensions;
using Granit.Analytics.Metrics;
using Granit.QueryEngine.Filtering;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Tests.Metrics;

public sealed class MetricDefinitionRegistrationTests
{
    [Fact]
    public void AddMetricDefinition_RegistersMetricAndDescriptor()
    {
        ServiceCollection services = new();

        services.AddMetricDefinition<Order, int, OrderCountMetric>();

        ServiceProvider provider = services.BuildServiceProvider();

        MetricDefinition<Order, int> definition = provider.GetRequiredService<MetricDefinition<Order, int>>();
        definition.ShouldBeOfType<OrderCountMetric>();

        IEnumerable<IMetricDefinitionDescriptor> descriptors = provider.GetServices<IMetricDefinitionDescriptor>();
        descriptors.ShouldContain(d => d.Name == "Sample.Order.Count");
    }

    [Fact]
    public void AddMetricDefinition_DescriptorAndDefinition_AreSameInstance()
    {
        ServiceCollection services = new();

        services.AddMetricDefinition<Order, int, OrderCountMetric>();

        ServiceProvider provider = services.BuildServiceProvider();
        MetricDefinition<Order, int> definition = provider.GetRequiredService<MetricDefinition<Order, int>>();
        IMetricDefinitionDescriptor descriptor = provider.GetServices<IMetricDefinitionDescriptor>()
            .Single(d => d.Name == "Sample.Order.Count");

        ReferenceEquals(definition, descriptor).ShouldBeTrue();
    }

    private sealed class Order
    {
        public int Id { get; init; }
    }

    private sealed class OrderCountMetric : MetricDefinition<Order, int>
    {
        public override string Name => "Sample.Order.Count";
        public override MetricValueKind ValueKind => MetricValueKind.Count;
        public override AggregateFunction Aggregation => AggregateFunction.Count;
        public override Expression<Func<Order, int?>>? Selector => null;
    }
}
