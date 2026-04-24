using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Pricing;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.Tests.Pricing;

/// <summary>
/// Pure-function tests for cumulative discount stacking. Boundaries: zero-amount
/// no-op, fixed-amount floor, percentage rounding (banker's, 4 decimals),
/// expiry skipping, trial no-op at billing time, multiple-discount stacking.
/// </summary>
public sealed class SubscriptionDiscountCalculatorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-04-24T12:00:00Z");

    private static SubscriptionDiscount NewDiscount(
        DiscountType type, decimal value, DateTimeOffset? expiresAt = null) =>
        SubscriptionDiscount.Create(Guid.NewGuid(), Guid.NewGuid(), type, value, "test", expiresAt);

    [Fact]
    public void Apply_NoDiscounts_ReturnsBaseAmount() =>
        SubscriptionDiscountCalculator.Apply(100m, [], Now).ShouldBe(100m);

    [Fact]
    public void Apply_SinglePercentage_ReducesProportionally() =>
        SubscriptionDiscountCalculator.Apply(100m,
            [NewDiscount(DiscountType.Percentage, 10m)], Now).ShouldBe(90m);

    [Fact]
    public void Apply_SingleFixedAmount_SubtractsAmount() =>
        SubscriptionDiscountCalculator.Apply(100m,
            [NewDiscount(DiscountType.FixedAmount, 25m)], Now).ShouldBe(75m);

    [Fact]
    public void Apply_FixedAmountExceedsTotal_FlooredAtZero() =>
        SubscriptionDiscountCalculator.Apply(50m,
            [NewDiscount(DiscountType.FixedAmount, 200m)], Now).ShouldBe(0m);

    [Fact]
    public void Apply_TrialDiscount_NoBillingImpact()
    {
        // Trial extension is handled elsewhere — calculator is a no-op.
        SubscriptionDiscountCalculator.Apply(100m,
            [NewDiscount(DiscountType.Trial, 7m)], Now).ShouldBe(100m);
    }

    [Fact]
    public void Apply_MultiplePercentages_StackCumulatively()
    {
        // 10% off then 10% off again = 0.9 × 0.9 = 0.81 = 81% of original = 19% effective discount.
        decimal result = SubscriptionDiscountCalculator.Apply(100m,
            [NewDiscount(DiscountType.Percentage, 10m), NewDiscount(DiscountType.Percentage, 10m)], Now);

        result.ShouldBe(81m);
    }

    [Fact]
    public void Apply_MixedFixedThenPercentage_AppliesInOrder()
    {
        // (100 - 20) × 0.9 = 72.
        decimal result = SubscriptionDiscountCalculator.Apply(100m,
            [NewDiscount(DiscountType.FixedAmount, 20m), NewDiscount(DiscountType.Percentage, 10m)], Now);

        result.ShouldBe(72m);
    }

    [Fact]
    public void Apply_MixedPercentageThenFixed_OrderMatters()
    {
        // (100 × 0.9) - 20 = 70 — different result vs the previous test, proving order matters.
        decimal result = SubscriptionDiscountCalculator.Apply(100m,
            [NewDiscount(DiscountType.Percentage, 10m), NewDiscount(DiscountType.FixedAmount, 20m)], Now);

        result.ShouldBe(70m);
    }

    [Fact]
    public void Apply_ExpiredDiscount_Skipped()
    {
        SubscriptionDiscount expired = NewDiscount(
            DiscountType.Percentage, 50m, expiresAt: Now.AddDays(-1));
        SubscriptionDiscount active = NewDiscount(DiscountType.Percentage, 10m);

        SubscriptionDiscountCalculator.Apply(100m, [expired, active], Now).ShouldBe(90m);
    }

    [Fact]
    public void Apply_TrialBetweenPriceDiscounts_DoesNotInterruptStacking()
    {
        decimal result = SubscriptionDiscountCalculator.Apply(100m,
            [
                NewDiscount(DiscountType.Percentage, 10m),
                NewDiscount(DiscountType.Trial, 30m),
                NewDiscount(DiscountType.FixedAmount, 5m),
            ], Now);

        result.ShouldBe(85m); // 100 → 90 → 90 → 85
    }

    [Fact]
    public void Apply_PercentageWithBankerRounding()
    {
        // 33.333% off 100 = 100 * (1 - 0.33333) = 66.667; 4-decimal banker's = 66.6670
        decimal result = SubscriptionDiscountCalculator.Apply(100m,
            [NewDiscount(DiscountType.Percentage, 33.333m)], Now);

        result.ShouldBe(66.667m);
    }

    [Fact]
    public void Apply_NegativeBaseAmount_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            SubscriptionDiscountCalculator.Apply(-1m, [], Now));

    [Fact]
    public void Apply_ZeroBaseAmount_StaysZero() =>
        SubscriptionDiscountCalculator.Apply(0m,
            [NewDiscount(DiscountType.Percentage, 50m)], Now).ShouldBe(0m);
}
