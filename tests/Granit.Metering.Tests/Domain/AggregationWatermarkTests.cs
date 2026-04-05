using Granit.Metering.Domain;
using Shouldly;
using Xunit;

namespace Granit.Metering.Tests.Domain;

public sealed class AggregationWatermarkTests
{
    // ======== Create ========

    [Fact]
    public void Create_ShouldInitializeWithEmptyLastProcessedEventId()
    {
        var id = Guid.NewGuid();
        var meterId = Guid.NewGuid();

        var watermark = AggregationWatermark.Create(id, meterId);

        watermark.Id.ShouldBe(id);
        watermark.MeterDefinitionId.ShouldBe(meterId);
        watermark.LastProcessedEventId.ShouldBe(Guid.Empty);
        watermark.LastProcessedAt.ShouldBeNull();
    }

    [Fact]
    public void Create_ShouldImplementIMultiTenant()
    {
        typeof(AggregationWatermark).GetInterfaces()
            .ShouldContain(i => i.Name == "IMultiTenant");
    }

    // ======== Advance ========

    [Fact]
    public void Advance_ShouldUpdateLastProcessedEventIdAndTimestamp()
    {
        var watermark = AggregationWatermark.Create(Guid.NewGuid(), Guid.NewGuid());
        var lastEventId = Guid.NewGuid();
        DateTimeOffset processedAt = DateTimeOffset.UtcNow;

        watermark.Advance(lastEventId, processedAt);

        watermark.LastProcessedEventId.ShouldBe(lastEventId);
        watermark.LastProcessedAt.ShouldBe(processedAt);
    }

    [Fact]
    public void Advance_CalledMultipleTimes_ShouldKeepLatestValues()
    {
        var watermark = AggregationWatermark.Create(Guid.NewGuid(), Guid.NewGuid());
        var firstEventId = Guid.NewGuid();
        var secondEventId = Guid.NewGuid();
        DateTimeOffset firstTime = DateTimeOffset.UtcNow;
        DateTimeOffset secondTime = firstTime.AddMinutes(5);

        watermark.Advance(firstEventId, firstTime);
        watermark.Advance(secondEventId, secondTime);

        watermark.LastProcessedEventId.ShouldBe(secondEventId);
        watermark.LastProcessedAt.ShouldBe(secondTime);
    }
}
