using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Pricing;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.Tests.Pricing;

/// <summary>
/// Pure-function tests for the tier total computation. Boundary semantics, single
/// tier, exact match, exceeding all tiers, zero quantity, and the FlatAmount
/// addition are all exercised.
/// </summary>
public sealed class TieredPricingCalculatorTests
{
    private static PricingTier Tier(int sortOrder, decimal? upTo, decimal unit, decimal? flat = null) =>
        PricingTier.Create(Guid.NewGuid(), Guid.NewGuid(), sortOrder, upTo, unit, flat);

    // ──────────────── Volume ────────────────

    /// <summary>
    /// Volume mode: the unit price of the active tier applies to the entire quantity.
    /// 50K calls fall in tier 2 (10K-100K) so the whole 50K is charged at tier 2's rate.
    /// </summary>
    [Fact]
    public void Volume_QuantityInMiddleTier_ChargesEntireQuantityAtThatTier()
    {
        IReadOnlyList<PricingTier> tiers = [
            Tier(0, 10_000m, 0.10m),
            Tier(1, 100_000m, 0.05m),
            Tier(2, null,      0.02m),
        ];

        decimal total = TieredPricingCalculator.Compute(50_000m, TieringMode.Volume, tiers);

        total.ShouldBe(50_000m * 0.05m); // = 2_500
    }

    [Fact]
    public void Volume_QuantityExactlyAtBoundary_BelongsToLowerTier()
    {
        // Boundary is tier-inclusive: a quantity of exactly 10_000 still belongs to tier 0.
        IReadOnlyList<PricingTier> tiers = [
            Tier(0, 10_000m, 0.10m),
            Tier(1, null,    0.05m),
        ];

        decimal total = TieredPricingCalculator.Compute(10_000m, TieringMode.Volume, tiers);

        total.ShouldBe(10_000m * 0.10m); // = 1_000 — the cheaper tier owns its boundary
    }

    [Fact]
    public void Volume_QuantityAboveAllBoundaries_FallsIntoOpenEndedTier()
    {
        IReadOnlyList<PricingTier> tiers = [
            Tier(0, 10m, 0.10m),
            Tier(1, null, 0.02m),
        ];

        decimal total = TieredPricingCalculator.Compute(10_000m, TieringMode.Volume, tiers);

        total.ShouldBe(10_000m * 0.02m);
    }

    [Fact]
    public void Volume_FlatAmount_AddedToActiveTier()
    {
        IReadOnlyList<PricingTier> tiers = [
            Tier(0, 100m, 0.10m, flat: 5m),
            Tier(1, null, 0.05m),
        ];

        decimal total = TieredPricingCalculator.Compute(50m, TieringMode.Volume, tiers);

        total.ShouldBe((50m * 0.10m) + 5m); // = 10
    }

    // ──────────────── Graduated ────────────────

    /// <summary>
    /// Graduated mode: each bracket's units are charged at that bracket's rate.
    /// 50K calls = 10K @ 0.10 + 40K @ 0.05 = 1_000 + 2_000 = 3_000.
    /// </summary>
    [Fact]
    public void Graduated_QuantitySpanningTwoTiers_SumsPerBracket()
    {
        IReadOnlyList<PricingTier> tiers = [
            Tier(0, 10_000m,  0.10m),
            Tier(1, 100_000m, 0.05m),
            Tier(2, null,      0.02m),
        ];

        decimal total = TieredPricingCalculator.Compute(50_000m, TieringMode.Graduated, tiers);

        total.ShouldBe((10_000m * 0.10m) + (40_000m * 0.05m)); // = 3_000
    }

    [Fact]
    public void Graduated_QuantitySpanningAllThreeTiers_SumsPerBracket()
    {
        IReadOnlyList<PricingTier> tiers = [
            Tier(0, 10_000m,  0.10m),
            Tier(1, 100_000m, 0.05m),
            Tier(2, null,      0.02m),
        ];

        decimal total = TieredPricingCalculator.Compute(150_000m, TieringMode.Graduated, tiers);

        total.ShouldBe(
            (10_000m * 0.10m) +    // tier 0:   1_000
            (90_000m * 0.05m) +    // tier 1:   4_500
            (50_000m * 0.02m));    // tier 2:   1_000
    }

    [Fact]
    public void Graduated_QuantityExactlyAtBoundary_StaysInLowerTier()
    {
        IReadOnlyList<PricingTier> tiers = [
            Tier(0, 10_000m, 0.10m),
            Tier(1, null,    0.05m),
        ];

        decimal total = TieredPricingCalculator.Compute(10_000m, TieringMode.Graduated, tiers);

        total.ShouldBe(10_000m * 0.10m); // entirely in tier 0
    }

    [Fact]
    public void Graduated_FlatAmount_AddedPerCrossedBracket()
    {
        IReadOnlyList<PricingTier> tiers = [
            Tier(0, 100m, 0.10m, flat: 5m),
            Tier(1, null, 0.05m, flat: 2m),
        ];

        decimal total = TieredPricingCalculator.Compute(150m, TieringMode.Graduated, tiers);

        total.ShouldBe(
            (100m * 0.10m) + 5m +   // tier 0: 10 + 5 = 15
            (50m * 0.05m) + 2m);    // tier 1: 2.5 + 2 = 4.5  → total 19.5
    }

    // ──────────────── Edge cases ────────────────

    [Fact]
    public void ZeroQuantity_ReturnsZero_BothModes()
    {
        IReadOnlyList<PricingTier> tiers = [
            Tier(0, null, 0.10m, flat: 999m),
        ];

        TieredPricingCalculator.Compute(0m, TieringMode.Volume, tiers).ShouldBe(0m);
        TieredPricingCalculator.Compute(0m, TieringMode.Graduated, tiers).ShouldBe(0m);
    }

    [Fact]
    public void EmptyTierList_ReturnsZero()
    {
        TieredPricingCalculator.Compute(100m, TieringMode.Volume, []).ShouldBe(0m);
        TieredPricingCalculator.Compute(100m, TieringMode.Graduated, []).ShouldBe(0m);
    }

    [Fact]
    public void NegativeQuantity_Throws()
    {
        IReadOnlyList<PricingTier> tiers = [Tier(0, null, 0.10m)];

        Should.Throw<ArgumentOutOfRangeException>(() =>
            TieredPricingCalculator.Compute(-1m, TieringMode.Volume, tiers));
    }

    [Fact]
    public void SingleOpenEndedTier_FlatPerUnitAcrossModes()
    {
        IReadOnlyList<PricingTier> tiers = [Tier(0, null, 0.05m)];

        TieredPricingCalculator.Compute(1234m, TieringMode.Volume, tiers).ShouldBe(1234m * 0.05m);
        TieredPricingCalculator.Compute(1234m, TieringMode.Graduated, tiers).ShouldBe(1234m * 0.05m);
    }

    [Fact]
    public void OutOfOrderTierList_IsNormalisedBySortOrder()
    {
        // Same brackets but supplied out of order — the calculator must re-sort.
        IReadOnlyList<PricingTier> tiers = [
            Tier(2, null,     0.02m),
            Tier(0, 10_000m,  0.10m),
            Tier(1, 100_000m, 0.05m),
        ];

        decimal total = TieredPricingCalculator.Compute(50_000m, TieringMode.Graduated, tiers);

        total.ShouldBe((10_000m * 0.10m) + (40_000m * 0.05m));
    }
}
