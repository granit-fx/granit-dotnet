using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.Tests;

public sealed class SubscriptionTests
{
    private static Subscription CreateTrialSubscription()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return Subscription.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PlanId.Create(Guid.NewGuid()),
            currency: "EUR",
            periodStart: now,
            periodEnd: now.AddMonths(1),
            billingCycleAnchor: now,
            trialEndsAt: now.AddDays(14));
    }

    private static Subscription CreateActiveSubscription()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return Subscription.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PlanId.Create(Guid.NewGuid()),
            currency: "EUR",
            periodStart: now,
            periodEnd: now.AddMonths(1),
            billingCycleAnchor: now);
    }

    [Fact]
    public void Create_WithTrial_ShouldSetTrialStatus()
    {
        Subscription sub = CreateTrialSubscription();

        sub.Status.ShouldBe(SubscriptionStatus.Trial);
        sub.TrialEndsAt.ShouldNotBeNull();
    }

    [Fact]
    public void Create_WithoutTrial_ShouldSetActiveStatus()
    {
        Subscription sub = CreateActiveSubscription();

        sub.Status.ShouldBe(SubscriptionStatus.Active);
    }

    [Fact]
    public void Activate_FromTrial_ShouldReturnTrue()
    {
        Subscription sub = CreateTrialSubscription();

        bool result = sub.Activate();

        result.ShouldBeTrue();
        sub.Status.ShouldBe(SubscriptionStatus.Active);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ShouldReturnFalse()
    {
        Subscription sub = CreateActiveSubscription();

        bool result = sub.Activate();

        result.ShouldBeFalse();
    }

    [Fact]
    public void Cancel_FromActive_ShouldReturnTrue()
    {
        Subscription sub = CreateActiveSubscription();
        DateTimeOffset cancelledAt = DateTimeOffset.UtcNow;

        bool result = sub.Cancel("No longer needed", cancelledAt);

        result.ShouldBeTrue();
        sub.Status.ShouldBe(SubscriptionStatus.Cancelled);
        sub.CancellationReason.ShouldBe("No longer needed");
        sub.CancelledAt.ShouldBe(cancelledAt);
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ShouldReturnFalse()
    {
        Subscription sub = CreateActiveSubscription();
        sub.Cancel("reason", DateTimeOffset.UtcNow);

        bool result = sub.Cancel("again", DateTimeOffset.UtcNow);

        result.ShouldBeFalse();
    }

    [Fact]
    public void ScheduleCancelAtPeriodEnd_ShouldSetFlag()
    {
        Subscription sub = CreateActiveSubscription();

        sub.ScheduleCancelAtPeriodEnd();

        sub.CancelAtPeriodEnd.ShouldBeTrue();
    }

    [Fact]
    public void UnscheduleCancelAtPeriodEnd_ShouldClearFlag()
    {
        Subscription sub = CreateActiveSubscription();
        sub.ScheduleCancelAtPeriodEnd();

        sub.UnscheduleCancelAtPeriodEnd();

        sub.CancelAtPeriodEnd.ShouldBeFalse();
    }

    [Fact]
    public void Expire_FromTrial_ShouldReturnTrue()
    {
        Subscription sub = CreateTrialSubscription();

        bool result = sub.Expire();

        result.ShouldBeTrue();
        sub.Status.ShouldBe(SubscriptionStatus.Expired);
    }

    [Fact]
    public void MarkPastDue_FromActive_ShouldReturnTrue()
    {
        Subscription sub = CreateActiveSubscription();

        bool result = sub.MarkPastDue();

        result.ShouldBeTrue();
        sub.Status.ShouldBe(SubscriptionStatus.PastDue);
    }

    [Fact]
    public void Suspend_FromPastDue_ShouldReturnTrue()
    {
        Subscription sub = CreateActiveSubscription();
        sub.MarkPastDue();

        bool result = sub.Suspend();

        result.ShouldBeTrue();
        sub.Status.ShouldBe(SubscriptionStatus.Suspended);
    }

    [Fact]
    public void ChangePlan_WhenActive_ShouldUpdatePlanId()
    {
        Subscription sub = CreateActiveSubscription();
        var newPlanId = PlanId.Create(Guid.NewGuid());

        sub.ChangePlan(newPlanId);

        sub.PlanId.ShouldBe(newPlanId);
    }

    [Fact]
    public void PlanId_Create_WithEmptyGuid_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => PlanId.Create(Guid.Empty));
    }

    [Fact]
    public void PlanId_ImplicitConversion_ShouldRoundTrip()
    {
        var guid = Guid.NewGuid();
        PlanId id = guid;
        Guid result = id;

        result.ShouldBe(guid);
    }

    [Fact]
    public void SubscriptionId_Create_WithEmptyGuid_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => SubscriptionId.Create(Guid.Empty));
    }
}
