using Granit.Subscriptions.Domain;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.Tests.Domain;

public sealed class SubscriptionPriceOverrideTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
    private static readonly DateTimeOffset T30 = T0.AddDays(30);

    [Fact]
    public void Create_WithAllFields_StoresThem()
    {
        var id = Guid.NewGuid();
        var subId = Guid.NewGuid();
        var priceId = Guid.NewGuid();

        var ov = SubscriptionPriceOverride.Create(
            id, subId, priceId, amount: 49.99m,
            effectiveFrom: T0, effectiveUntil: T30,
            reason: "Acme MSA #5678");

        ov.Id.ShouldBe(id);
        ov.SubscriptionId.ShouldBe(subId);
        ov.PlanPriceId.ShouldBe(priceId);
        ov.Amount.ShouldBe(49.99m);
        ov.EffectiveFrom.ShouldBe(T0);
        ov.EffectiveUntil.ShouldBe(T30);
        ov.Reason.ShouldBe("Acme MSA #5678");
    }

    [Fact]
    public void Create_NegativeAmount_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            SubscriptionPriceOverride.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                amount: -1m, effectiveFrom: T0, effectiveUntil: null, reason: "x"));

    [Fact]
    public void Create_EffectiveUntilEqualsFrom_Throws() =>
        Should.Throw<ArgumentException>(() =>
            SubscriptionPriceOverride.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                amount: 10m, effectiveFrom: T0, effectiveUntil: T0, reason: "x"));

    [Fact]
    public void Create_EffectiveUntilBeforeFrom_Throws() =>
        Should.Throw<ArgumentException>(() =>
            SubscriptionPriceOverride.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                amount: 10m, effectiveFrom: T30, effectiveUntil: T0, reason: "x"));

    [Fact]
    public void Create_EmptyReason_Throws() =>
        Should.Throw<ArgumentException>(() =>
            SubscriptionPriceOverride.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                amount: 10m, effectiveFrom: T0, effectiveUntil: null, reason: " "));

    // ──────────── Covers semantics ────────────

    [Fact]
    public void Covers_MatchingPriceInsideWindow_True()
    {
        var priceId = Guid.NewGuid();
        var ov = SubscriptionPriceOverride.Create(
            Guid.NewGuid(), Guid.NewGuid(), priceId, 10m, T0, T30, "x");

        ov.Covers(priceId, T0.AddDays(15)).ShouldBeTrue();
    }

    [Fact]
    public void Covers_DifferentPriceId_False()
    {
        var priceId = Guid.NewGuid();
        var ov = SubscriptionPriceOverride.Create(
            Guid.NewGuid(), Guid.NewGuid(), priceId, 10m, T0, T30, "x");

        ov.Covers(Guid.NewGuid(), T0.AddDays(15)).ShouldBeFalse();
    }

    [Fact]
    public void Covers_AtEffectiveUntil_FalseExclusive()
    {
        var priceId = Guid.NewGuid();
        var ov = SubscriptionPriceOverride.Create(
            Guid.NewGuid(), Guid.NewGuid(), priceId, 10m, T0, T30, "x");

        ov.Covers(priceId, T30).ShouldBeFalse();
    }

    [Fact]
    public void Covers_OpenEnded_CoversAnyInstantAfterFrom()
    {
        var priceId = Guid.NewGuid();
        var ov = SubscriptionPriceOverride.Create(
            Guid.NewGuid(), Guid.NewGuid(), priceId, 10m,
            effectiveFrom: T0, effectiveUntil: null, reason: "x");

        ov.Covers(priceId, T0).ShouldBeTrue();
        ov.Covers(priceId, T30.AddYears(50)).ShouldBeTrue();
        ov.Covers(priceId, T0.AddSeconds(-1)).ShouldBeFalse();
    }
}
