using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.Tests.Domain;

public sealed class SubscriptionPhaseTests
{
    private static readonly PlanId Plan = PlanId.Create(Guid.NewGuid());
    private static readonly DateTimeOffset T0 = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
    private static readonly DateTimeOffset T30 = T0.AddDays(30);
    private static readonly DateTimeOffset T60 = T0.AddDays(60);

    private static SubscriptionPhase Phase(Guid subId, DateTimeOffset start, DateTimeOffset? endDate) =>
        SubscriptionPhase.Create(Guid.NewGuid(), subId, start, endDate, Plan);

    // ────────── Factory validation ──────────

    [Fact]
    public void Create_WithEndDateBeforeStartDate_Throws() =>
        Should.Throw<ArgumentException>(() =>
            SubscriptionPhase.Create(Guid.NewGuid(), Guid.NewGuid(), T30, T0, Plan));

    [Fact]
    public void Create_WithEndDateEqualsStartDate_Throws() =>
        Should.Throw<ArgumentException>(() =>
            SubscriptionPhase.Create(Guid.NewGuid(), Guid.NewGuid(), T0, T0, Plan));

    [Fact]
    public void Create_WithDiscountAbove100_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            SubscriptionPhase.Create(Guid.NewGuid(), Guid.NewGuid(), T0, null, Plan, discountPercent: 100.1m));

    [Fact]
    public void Create_WithNegativeDiscount_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            SubscriptionPhase.Create(Guid.NewGuid(), Guid.NewGuid(), T0, null, Plan, discountPercent: -1m));

    [Fact]
    public void Create_WithDiscountAtBoundary_Allowed()
    {
        var a = SubscriptionPhase.Create(Guid.NewGuid(), Guid.NewGuid(), T0, null, Plan, discountPercent: 0m);
        var b = SubscriptionPhase.Create(Guid.NewGuid(), Guid.NewGuid(), T0, null, Plan, discountPercent: 100m);

        a.DiscountPercent.ShouldBe(0m);
        b.DiscountPercent.ShouldBe(100m);
    }

    // ────────── Covers semantics (half-open interval) ──────────

    [Fact]
    public void Covers_AtStart_IsInclusive()
    {
        SubscriptionPhase phase = Phase(Guid.NewGuid(), T0, T30);

        phase.Covers(T0).ShouldBeTrue();
    }

    [Fact]
    public void Covers_AtEnd_IsExclusive()
    {
        SubscriptionPhase phase = Phase(Guid.NewGuid(), T0, T30);

        phase.Covers(T30).ShouldBeFalse();
    }

    [Fact]
    public void Covers_BeforeStart_False()
    {
        SubscriptionPhase phase = Phase(Guid.NewGuid(), T30, T60);

        phase.Covers(T0).ShouldBeFalse();
    }

    [Fact]
    public void Covers_OpenEnded_CoversAnyInstantAfterStart()
    {
        SubscriptionPhase phase = Phase(Guid.NewGuid(), T30, endDate: null);

        phase.Covers(T30).ShouldBeTrue();
        phase.Covers(T60).ShouldBeTrue();
        phase.Covers(T60.AddYears(50)).ShouldBeTrue();
        phase.Covers(T0).ShouldBeFalse();
    }
}
