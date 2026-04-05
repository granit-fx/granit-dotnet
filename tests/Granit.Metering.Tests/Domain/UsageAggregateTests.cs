using Granit.Metering.Domain;
using Shouldly;
using Xunit;

namespace Granit.Metering.Tests.Domain;

public sealed class UsageAggregateTests
{
    // ======== Create ========

    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var id = Guid.NewGuid();
        var meterId = Guid.NewGuid();
        DateTimeOffset start = DateTimeOffset.UtcNow;
        DateTimeOffset end = start.AddHours(1);

        var aggregate = UsageAggregate.Create(
            id, meterId, AggregationPeriod.Hourly, start, end, 1500.5m, 42);

        aggregate.Id.ShouldBe(id);
        aggregate.MeterDefinitionId.ShouldBe(meterId);
        aggregate.Period.ShouldBe(AggregationPeriod.Hourly);
        aggregate.PeriodStart.ShouldBe(start);
        aggregate.PeriodEnd.ShouldBe(end);
        aggregate.AggregatedValue.ShouldBe(1500.5m);
        aggregate.EventCount.ShouldBe(42);
    }

    [Fact]
    public void Create_WithZeroValues_ShouldSucceed()
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;

        var aggregate = UsageAggregate.Create(
            Guid.NewGuid(), Guid.NewGuid(), AggregationPeriod.Daily, start, start.AddDays(1), 0m, 0);

        aggregate.AggregatedValue.ShouldBe(0m);
        aggregate.EventCount.ShouldBe(0);
    }

    [Fact]
    public void Create_ShouldImplementIMultiTenant()
    {
        typeof(UsageAggregate).GetInterfaces()
            .ShouldContain(i => i.Name == "IMultiTenant");
    }

    // ======== Recompute ========

    [Fact]
    public void Recompute_ShouldUpdateValues()
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;
        var aggregate = UsageAggregate.Create(
            Guid.NewGuid(), Guid.NewGuid(), AggregationPeriod.Hourly, start, start.AddHours(1), 100m, 10);

        aggregate.Recompute(250m, 25);

        aggregate.AggregatedValue.ShouldBe(250m);
        aggregate.EventCount.ShouldBe(25);
    }

    [Fact]
    public void Recompute_ShouldNotChangePeriodOrMeter()
    {
        var meterId = Guid.NewGuid();
        DateTimeOffset start = DateTimeOffset.UtcNow;
        var aggregate = UsageAggregate.Create(
            Guid.NewGuid(), meterId, AggregationPeriod.BillingPeriod, start, start.AddDays(30), 100m, 10);

        aggregate.Recompute(999m, 99);

        aggregate.MeterDefinitionId.ShouldBe(meterId);
        aggregate.Period.ShouldBe(AggregationPeriod.BillingPeriod);
        aggregate.PeriodStart.ShouldBe(start);
    }
}
