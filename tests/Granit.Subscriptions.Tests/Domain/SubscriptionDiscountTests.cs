using Granit.Subscriptions.Domain;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.Tests.Domain;

public sealed class SubscriptionDiscountTests
{
    [Fact]
    public void Create_WithAllFields_StoresThem()
    {
        var id = Guid.NewGuid();
        var subId = Guid.NewGuid();
        var expiry = DateTimeOffset.Parse("2027-01-01T00:00:00Z");

        var d = SubscriptionDiscount.Create(
            id, subId, DiscountType.Percentage, 10m,
            reason: "VIP customer agreement #4321",
            expiresAt: expiry);

        d.Id.ShouldBe(id);
        d.SubscriptionId.ShouldBe(subId);
        d.Type.ShouldBe(DiscountType.Percentage);
        d.Value.ShouldBe(10m);
        d.Reason.ShouldBe("VIP customer agreement #4321");
        d.ExpiresAt.ShouldBe(expiry);
    }

    [Fact]
    public void Create_PercentageOver100_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            SubscriptionDiscount.Create(Guid.NewGuid(), Guid.NewGuid(),
                DiscountType.Percentage, 100.5m, "x"));

    [Fact]
    public void Create_NegativeValue_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            SubscriptionDiscount.Create(Guid.NewGuid(), Guid.NewGuid(),
                DiscountType.FixedAmount, -1m, "x"));

    [Fact]
    public void Create_TrialWithFractionalValue_Throws() =>
        Should.Throw<ArgumentException>(() =>
            SubscriptionDiscount.Create(Guid.NewGuid(), Guid.NewGuid(),
                DiscountType.Trial, 7.5m, "extension"));

    [Fact]
    public void Create_EmptyReason_Throws() =>
        Should.Throw<ArgumentException>(() =>
            SubscriptionDiscount.Create(Guid.NewGuid(), Guid.NewGuid(),
                DiscountType.Percentage, 10m, "  "));

    [Fact]
    public void Create_ReasonOverMaxLength_Throws()
    {
        string overLong = new('x', SubscriptionDiscount.ReasonMaxLength + 1);
        Should.Throw<ArgumentOutOfRangeException>(() =>
            SubscriptionDiscount.Create(Guid.NewGuid(), Guid.NewGuid(),
                DiscountType.FixedAmount, 1m, overLong));
    }

    [Fact]
    public void IsActiveAt_NoExpiry_AlwaysTrue()
    {
        var d = SubscriptionDiscount.Create(
            Guid.NewGuid(), Guid.NewGuid(), DiscountType.Percentage, 10m, "x");

        d.IsActiveAt(DateTimeOffset.UtcNow.AddYears(50)).ShouldBeTrue();
    }

    [Fact]
    public void IsActiveAt_BeforeExpiry_True()
    {
        var expiry = DateTimeOffset.Parse("2027-01-01T00:00:00Z");
        var d = SubscriptionDiscount.Create(
            Guid.NewGuid(), Guid.NewGuid(), DiscountType.Percentage, 10m,
            "x", expiresAt: expiry);

        d.IsActiveAt(expiry.AddSeconds(-1)).ShouldBeTrue();
    }

    [Fact]
    public void IsActiveAt_AtOrAfterExpiry_False()
    {
        var expiry = DateTimeOffset.Parse("2027-01-01T00:00:00Z");
        var d = SubscriptionDiscount.Create(
            Guid.NewGuid(), Guid.NewGuid(), DiscountType.Percentage, 10m,
            "x", expiresAt: expiry);

        d.IsActiveAt(expiry).ShouldBeFalse();              // exclusive
        d.IsActiveAt(expiry.AddDays(1)).ShouldBeFalse();
    }
}
