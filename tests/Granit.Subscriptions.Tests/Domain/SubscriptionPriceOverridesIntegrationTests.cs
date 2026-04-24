using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.Tests.Domain;

public sealed class SubscriptionPriceOverridesIntegrationTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
    private static readonly DateTimeOffset T30 = T0.AddDays(30);

    private static Subscription NewSubscription() => Subscription.Create(
        SubscriptionId.Create(Guid.NewGuid()),
        Guid.NewGuid(),
        PlanId.Create(Guid.NewGuid()),
        currency: "EUR",
        period: new SubscriptionPeriod(T0, T0.AddMonths(12), BillingCycleAnchor: T0));

    [Fact]
    public void AddPriceOverride_StoresAndIsRetrievable()
    {
        Subscription sub = NewSubscription();
        var priceId = Guid.NewGuid();
        var ov = SubscriptionPriceOverride.Create(
            Guid.NewGuid(), sub.Id, priceId, 49m, T0, T30, "Acme MSA");

        sub.AddPriceOverride(ov);

        sub.PriceOverrides.Count.ShouldBe(1);
        sub.GetActivePriceOverride(priceId, T0.AddDays(15))!.Amount.ShouldBe(49m);
    }

    [Fact]
    public void AddPriceOverride_MismatchedSubscriptionId_Throws()
    {
        Subscription sub = NewSubscription();
        var stray = SubscriptionPriceOverride.Create(
            Guid.NewGuid(),
            subscriptionId: Guid.NewGuid(),
            planPriceId: Guid.NewGuid(),
            amount: 1m, effectiveFrom: T0, effectiveUntil: null, reason: "x");

        Should.Throw<InvalidOperationException>(() => sub.AddPriceOverride(stray));
    }

    [Fact]
    public void GetActivePriceOverride_MissingPriceMatch_ReturnsNull()
    {
        Subscription sub = NewSubscription();
        var priceA = Guid.NewGuid();
        sub.AddPriceOverride(SubscriptionPriceOverride.Create(
            Guid.NewGuid(), sub.Id, priceA, 49m, T0, null, "x"));

        sub.GetActivePriceOverride(Guid.NewGuid(), T0.AddDays(1)).ShouldBeNull();
    }

    [Fact]
    public void GetActivePriceOverride_OutsideWindow_ReturnsNull()
    {
        Subscription sub = NewSubscription();
        var priceId = Guid.NewGuid();
        sub.AddPriceOverride(SubscriptionPriceOverride.Create(
            Guid.NewGuid(), sub.Id, priceId, 49m, T0, T30, "x"));

        sub.GetActivePriceOverride(priceId, T30).ShouldBeNull();          // exclusive end
        sub.GetActivePriceOverride(priceId, T0.AddDays(-1)).ShouldBeNull();
    }

    [Fact]
    public void GetActivePriceOverride_MultipleNonOverlapping_PicksMatchingWindow()
    {
        Subscription sub = NewSubscription();
        var priceId = Guid.NewGuid();
        sub.AddPriceOverride(SubscriptionPriceOverride.Create(
            Guid.NewGuid(), sub.Id, priceId, 50m, T0, T30, "intro"));
        sub.AddPriceOverride(SubscriptionPriceOverride.Create(
            Guid.NewGuid(), sub.Id, priceId, 75m, T30, null, "renewal"));

        sub.GetActivePriceOverride(priceId, T0.AddDays(15))!.Amount.ShouldBe(50m);
        sub.GetActivePriceOverride(priceId, T30.AddDays(15))!.Amount.ShouldBe(75m);
    }

    [Fact]
    public void RemovePriceOverride_ExistingId_RemovesAndReturnsTrue()
    {
        Subscription sub = NewSubscription();
        var ov = SubscriptionPriceOverride.Create(
            Guid.NewGuid(), sub.Id, Guid.NewGuid(), 49m, T0, T30, "x");
        sub.AddPriceOverride(ov);

        sub.RemovePriceOverride(ov.Id).ShouldBeTrue();
        sub.PriceOverrides.Count.ShouldBe(0);
    }

    [Fact]
    public void RemovePriceOverride_UnknownId_ReturnsFalse()
    {
        Subscription sub = NewSubscription();

        sub.RemovePriceOverride(Guid.NewGuid()).ShouldBeFalse();
    }
}
