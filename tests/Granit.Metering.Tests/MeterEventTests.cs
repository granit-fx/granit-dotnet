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

    // ======== Soft deprecation ========

    [Fact]
    public void Create_NewEvent_IsNotDeprecated()
    {
        var evt = MeterEvent.Create(
            Guid.NewGuid(), Guid.NewGuid(), "key-fresh", 1m, DateTimeOffset.UtcNow);

        evt.DeprecatedAt.ShouldBeNull();
        evt.DeprecationReason.ShouldBeNull();
    }

    [Fact]
    public void Deprecate_SetsTimestampAndReason()
    {
        var evt = MeterEvent.Create(
            Guid.NewGuid(), Guid.NewGuid(), "key-x", 1m, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.Parse("2026-04-24T15:30:00Z");

        evt.Deprecate("billing-fix #123", now);

        evt.DeprecatedAt.ShouldBe(now);
        evt.DeprecationReason.ShouldBe("billing-fix #123");
    }

    [Fact]
    public void Deprecate_TwiceOnSameEvent_ShouldThrow()
    {
        var evt = MeterEvent.Create(
            Guid.NewGuid(), Guid.NewGuid(), "key-twice", 1m, DateTimeOffset.UtcNow);
        evt.Deprecate("first", DateTimeOffset.UtcNow);

        Should.Throw<InvalidOperationException>(() =>
            evt.Deprecate("second", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Deprecate_WithEmptyReason_ShouldThrow()
    {
        var evt = MeterEvent.Create(
            Guid.NewGuid(), Guid.NewGuid(), "key-empty", 1m, DateTimeOffset.UtcNow);

        Should.Throw<ArgumentException>(() => evt.Deprecate("  ", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Deprecate_WithReasonOverMaxLength_ShouldThrow()
    {
        var evt = MeterEvent.Create(
            Guid.NewGuid(), Guid.NewGuid(), "key-long", 1m, DateTimeOffset.UtcNow);
        string overLong = new('x', MeterEvent.DeprecationReasonMaxLength + 1);

        Should.Throw<ArgumentOutOfRangeException>(() =>
            evt.Deprecate(overLong, DateTimeOffset.UtcNow));
    }
}
