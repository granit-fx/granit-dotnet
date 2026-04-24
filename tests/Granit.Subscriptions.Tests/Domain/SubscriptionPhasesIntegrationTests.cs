using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.Tests.Domain;

/// <summary>
/// Subscription ↔ SubscriptionPhase wiring: AddPhase overlap rules,
/// GetActivePhase point lookups, and the boundary-touching exception.
/// </summary>
public sealed class SubscriptionPhasesIntegrationTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
    private static readonly DateTimeOffset T30 = T0.AddDays(30);
    private static readonly DateTimeOffset T60 = T0.AddDays(60);

    private static Subscription NewSubscription() => Subscription.Create(
        SubscriptionId.Create(Guid.NewGuid()),
        Guid.NewGuid(),
        PlanId.Create(Guid.NewGuid()),
        currency: "EUR",
        period: new SubscriptionPeriod(T0, T0.AddMonths(12), BillingCycleAnchor: T0));

    private static SubscriptionPhase Phase(Subscription sub, DateTimeOffset start, DateTimeOffset? endDate) =>
        SubscriptionPhase.Create(Guid.NewGuid(), sub.Id, start, endDate, PlanId.Create(Guid.NewGuid()));

    [Fact]
    public void AddPhase_FirstPhase_Succeeds()
    {
        Subscription sub = NewSubscription();

        sub.AddPhase(Phase(sub, T0, T30));

        sub.Phases.Count.ShouldBe(1);
    }

    [Fact]
    public void AddPhase_AdjacentTouchingEndpoints_Succeeds()
    {
        // Phase A: [T0, T30); Phase B: [T30, T60). The boundary at T30 belongs to B
        // (start is inclusive, end is exclusive) — they do NOT overlap.
        Subscription sub = NewSubscription();
        sub.AddPhase(Phase(sub, T0, T30));

        Should.NotThrow(() => sub.AddPhase(Phase(sub, T30, T60)));

        sub.Phases.Count.ShouldBe(2);
    }

    [Fact]
    public void AddPhase_OverlappingExisting_Throws()
    {
        Subscription sub = NewSubscription();
        sub.AddPhase(Phase(sub, T0, T60));

        Should.Throw<InvalidOperationException>(() =>
            sub.AddPhase(Phase(sub, T30, T60.AddDays(30))));
    }

    [Fact]
    public void AddPhase_SecondOpenEndedPhase_OverlapsExistingOpenEnded_Throws()
    {
        Subscription sub = NewSubscription();
        sub.AddPhase(Phase(sub, T0, endDate: null));

        Should.Throw<InvalidOperationException>(() =>
            sub.AddPhase(Phase(sub, T30, endDate: null)));
    }

    [Fact]
    public void AddPhase_OpenEndedAfterClosed_Succeeds()
    {
        Subscription sub = NewSubscription();
        sub.AddPhase(Phase(sub, T0, T30));

        Should.NotThrow(() => sub.AddPhase(Phase(sub, T30, endDate: null)));

        sub.Phases.Count.ShouldBe(2);
    }

    [Fact]
    public void AddPhase_WithMismatchedSubscriptionId_Throws()
    {
        Subscription sub = NewSubscription();
        var strayPhase = SubscriptionPhase.Create(
            Guid.NewGuid(), subscriptionId: Guid.NewGuid(), T0, T30,
            PlanId.Create(Guid.NewGuid()));

        Should.Throw<InvalidOperationException>(() => sub.AddPhase(strayPhase));
    }

    [Fact]
    public void GetActivePhase_AtInstantInsidePhase_ReturnsThatPhase()
    {
        Subscription sub = NewSubscription();
        SubscriptionPhase trial = Phase(sub, T0, T30);
        SubscriptionPhase standard = Phase(sub, T30, endDate: null);
        sub.AddPhase(trial);
        sub.AddPhase(standard);

        sub.GetActivePhase(T0.AddDays(15))!.Id.ShouldBe(trial.Id);
        sub.GetActivePhase(T30)!.Id.ShouldBe(standard.Id);          // boundary belongs to standard
        sub.GetActivePhase(T60)!.Id.ShouldBe(standard.Id);
    }

    [Fact]
    public void GetActivePhase_OutsideAnyPhase_ReturnsNull()
    {
        Subscription sub = NewSubscription();
        sub.AddPhase(Phase(sub, T30, T60));

        sub.GetActivePhase(T0).ShouldBeNull();        // before all phases
        sub.GetActivePhase(T60).ShouldBeNull();       // exactly at exclusive end
    }

    [Fact]
    public void GetActivePhase_NoPhases_ReturnsNull()
    {
        Subscription sub = NewSubscription();

        sub.GetActivePhase(T30).ShouldBeNull();
    }

    [Fact]
    public void RemovePhase_ExistingId_RemovesAndReturnsTrue()
    {
        Subscription sub = NewSubscription();
        SubscriptionPhase phase = Phase(sub, T0, T30);
        sub.AddPhase(phase);

        sub.RemovePhase(phase.Id).ShouldBeTrue();
        sub.Phases.Count.ShouldBe(0);
    }

    [Fact]
    public void RemovePhase_UnknownId_ReturnsFalse()
    {
        Subscription sub = NewSubscription();
        sub.AddPhase(Phase(sub, T0, T30));

        sub.RemovePhase(Guid.NewGuid()).ShouldBeFalse();
        sub.Phases.Count.ShouldBe(1);
    }
}
