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

    // ======== Quantity validation ========

    [Fact]
    public void Create_WithZeroQuantity_ShouldThrow()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            MeterEvent.Create(Guid.NewGuid(), Guid.NewGuid(), "key-1", 0m, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_WithNegativeQuantity_ShouldThrow()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            MeterEvent.Create(Guid.NewGuid(), Guid.NewGuid(), "key-1", -5m, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_ExceedingMaxQuantity_ShouldThrow()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            MeterEvent.Create(Guid.NewGuid(), Guid.NewGuid(), "key-1", MeterEvent.MaxQuantity + 1m, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_AtMaxQuantity_ShouldSucceed()
    {
        var evt = MeterEvent.Create(
            Guid.NewGuid(), Guid.NewGuid(), "key-1", MeterEvent.MaxQuantity, DateTimeOffset.UtcNow);

        evt.Quantity.ShouldBe(MeterEvent.MaxQuantity);
    }

    // ======== Null metadata ========

    [Fact]
    public void Create_WithNullMetadata_ShouldSucceed()
    {
        var evt = MeterEvent.Create(
            Guid.NewGuid(), Guid.NewGuid(), "key-1", 1m, DateTimeOffset.UtcNow, metadata: null);

        evt.Metadata.ShouldBeNull();
    }
}
