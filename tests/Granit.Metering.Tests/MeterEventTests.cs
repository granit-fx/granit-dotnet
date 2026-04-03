using Granit.Metering.Domain;
using Shouldly;
using Xunit;

namespace Granit.Metering.Tests;

public sealed class MeterEventTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        var meterId = Guid.NewGuid();
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        var evt = MeterEvent.Create(
            Guid.NewGuid(), meterId, "req-123", 1m, timestamp, """{"endpoint":"/api"}""");

        evt.MeterDefinitionId.ShouldBe(meterId);
        evt.IdempotencyKey.ShouldBe("req-123");
        evt.Quantity.ShouldBe(1m);
        evt.Timestamp.ShouldBe(timestamp);
        evt.Metadata.ShouldBe("""{"endpoint":"/api"}""");
    }

    [Fact]
    public void Create_WithNullIdempotencyKey_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() =>
            MeterEvent.Create(Guid.NewGuid(), Guid.NewGuid(), null!, 1m, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_WithEmptyIdempotencyKey_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() =>
            MeterEvent.Create(Guid.NewGuid(), Guid.NewGuid(), "", 1m, DateTimeOffset.UtcNow));
    }
}
