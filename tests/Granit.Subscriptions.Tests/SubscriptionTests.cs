using Granit.Parties.Domain.ValueObjects;
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
            PartyId.Create(Guid.NewGuid()),
            PlanId.Create(Guid.NewGuid()),
            currency: "EUR",
            new SubscriptionPeriod(now, now.AddMonths(1), BillingCycleAnchor: now),
            trialEndsAt: now.AddDays(14));
    }

    private static Subscription CreateActiveSubscription()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return Subscription.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PartyId.Create(Guid.NewGuid()),
            PlanId.Create(Guid.NewGuid()),
            currency: "EUR",
            new SubscriptionPeriod(now, now.AddMonths(1), BillingCycleAnchor: now));
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
    public void PlanId_Create_WithEmptyGuid_ShouldThrow() =>
        Should.Throw<ArgumentException>(() => PlanId.Create(Guid.Empty));

    [Fact]
    public void PlanId_ImplicitConversion_ShouldRoundTrip()
    {
        var guid = Guid.NewGuid();
        PlanId id = guid;
        Guid result = id;

        result.ShouldBe(guid);
    }

    [Fact]
    public void SubscriptionId_Create_WithEmptyGuid_ShouldThrow() =>
        Should.Throw<ArgumentException>(() => SubscriptionId.Create(Guid.Empty));

    // ── Price migration tests ─────────────────────────────────────

    [Fact]
    public void Create_WithPlanPriceId_ShouldPinPrice()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var priceId = Guid.NewGuid();

        var sub = Subscription.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PartyId.Create(Guid.NewGuid()),
            PlanId.Create(Guid.NewGuid()),
            currency: "EUR",
            new SubscriptionPeriod(now, now.AddMonths(1), BillingCycleAnchor: now),
            planPriceId: priceId);

        sub.PlanPriceId.ShouldBe(priceId);
    }

    [Fact]
    public void Create_WithoutPlanPriceId_ShouldLeaveNull()
    {
        Subscription sub = CreateActiveSubscription();

        sub.PlanPriceId.ShouldBeNull();
    }

    [Fact]
    public void MigratePrice_WhenActive_ShouldReturnTrue()
    {
        Subscription sub = CreateActiveSubscription();
        var newPriceId = Guid.NewGuid();

        bool result = sub.MigratePrice(newPriceId);

        result.ShouldBeTrue();
        sub.PlanPriceId.ShouldBe(newPriceId);
    }

    [Fact]
    public void MigratePrice_SamePrice_ShouldReturnFalse()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var priceId = Guid.NewGuid();

        var sub = Subscription.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PartyId.Create(Guid.NewGuid()),
            PlanId.Create(Guid.NewGuid()),
            currency: "EUR",
            new SubscriptionPeriod(now, now.AddMonths(1), BillingCycleAnchor: now),
            planPriceId: priceId);

        bool result = sub.MigratePrice(priceId);

        result.ShouldBeFalse();
    }

    [Fact]
    public void MigratePrice_WhenCancelled_ShouldThrow()
    {
        Subscription sub = CreateActiveSubscription();
        sub.Cancel("done", DateTimeOffset.UtcNow);

        Should.Throw<InvalidOperationException>(() => sub.MigratePrice(Guid.NewGuid()));
    }

    [Fact]
    public void MigratePrice_FromTrial_ShouldSucceed()
    {
        Subscription sub = CreateTrialSubscription();
        var newPriceId = Guid.NewGuid();

        bool result = sub.MigratePrice(newPriceId);

        result.ShouldBeTrue();
        sub.PlanPriceId.ShouldBe(newPriceId);
    }

    // ======== AdvancePeriod ========

    [Fact]
    public void AdvancePeriod_FromActive_ShouldUpdatePeriod()
    {
        Subscription sub = CreateActiveSubscription();
        DateTimeOffset newStart = sub.CurrentPeriodEnd;
        DateTimeOffset newEnd = newStart.AddMonths(1);

        sub.AdvancePeriod(newStart, newEnd);

        sub.CurrentPeriodStart.ShouldBe(newStart);
        sub.CurrentPeriodEnd.ShouldBe(newEnd);
    }

    [Fact]
    public void AdvancePeriod_FromTrial_ShouldSucceed()
    {
        Subscription sub = CreateTrialSubscription();
        DateTimeOffset newStart = sub.CurrentPeriodEnd;
        DateTimeOffset newEnd = newStart.AddMonths(1);

        sub.AdvancePeriod(newStart, newEnd);

        sub.CurrentPeriodStart.ShouldBe(newStart);
    }

    [Fact]
    public void AdvancePeriod_FromPastDue_ShouldSucceed()
    {
        Subscription sub = CreateActiveSubscription();
        sub.MarkPastDue();
        DateTimeOffset newStart = sub.CurrentPeriodEnd;
        DateTimeOffset newEnd = newStart.AddMonths(1);

        sub.AdvancePeriod(newStart, newEnd);

        sub.CurrentPeriodStart.ShouldBe(newStart);
    }

    [Fact]
    public void AdvancePeriod_FromCancelled_ShouldThrow()
    {
        Subscription sub = CreateActiveSubscription();
        sub.Cancel("done", DateTimeOffset.UtcNow);

        Should.Throw<InvalidOperationException>(() =>
            sub.AdvancePeriod(sub.CurrentPeriodEnd, sub.CurrentPeriodEnd.AddMonths(1)));
    }

    [Fact]
    public void AdvancePeriod_FromSuspended_ShouldThrow()
    {
        Subscription sub = CreateActiveSubscription();
        sub.MarkPastDue();
        sub.Suspend();

        Should.Throw<InvalidOperationException>(() =>
            sub.AdvancePeriod(sub.CurrentPeriodEnd, sub.CurrentPeriodEnd.AddMonths(1)));
    }

    [Fact]
    public void AdvancePeriod_StartBeforeCurrentEnd_ShouldThrow()
    {
        Subscription sub = CreateActiveSubscription();
        DateTimeOffset invalidStart = sub.CurrentPeriodEnd.AddDays(-1);

        Should.Throw<ArgumentOutOfRangeException>(() =>
            sub.AdvancePeriod(invalidStart, invalidStart.AddMonths(1)));
    }

    [Fact]
    public void AdvancePeriod_EndBeforeStart_ShouldThrow()
    {
        Subscription sub = CreateActiveSubscription();
        DateTimeOffset newStart = sub.CurrentPeriodEnd;

        Should.Throw<ArgumentOutOfRangeException>(() =>
            sub.AdvancePeriod(newStart, newStart.AddDays(-1)));
    }

    // ======== Seat management ========

    [Fact]
    public void AssignSeat_ShouldAddToCollection()
    {
        Subscription sub = CreateActiveSubscription();
        var seat = SubscriptionSeat.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

        sub.AssignSeat(seat);

        sub.Seats.Count.ShouldBe(1);
        sub.Seats[0].ShouldBe(seat);
    }

    [Fact]
    public void AssignSeat_WithNull_ShouldThrow()
    {
        Subscription sub = CreateActiveSubscription();

        Should.Throw<ArgumentNullException>(() => sub.AssignSeat(null!));
    }

    [Fact]
    public void RevokeSeat_ExistingUser_ShouldReturnTrue()
    {
        Subscription sub = CreateActiveSubscription();
        var userId = Guid.NewGuid();
        sub.AssignSeat(SubscriptionSeat.Create(Guid.NewGuid(), userId, DateTimeOffset.UtcNow));

        bool result = sub.RevokeSeat(userId);

        result.ShouldBeTrue();
        sub.Seats.Count.ShouldBe(0);
    }

    [Fact]
    public void RevokeSeat_NonExistentUser_ShouldReturnFalse()
    {
        Subscription sub = CreateActiveSubscription();

        bool result = sub.RevokeSeat(Guid.NewGuid());

        result.ShouldBeFalse();
    }

    // ======== External mappings ========

    [Fact]
    public void AddExternalMapping_ShouldAddToCollection()
    {
        Subscription sub = CreateActiveSubscription();
        var mapping = SubscriptionExternalMapping.Create(Guid.NewGuid(), "stripe", "sub_123");

        sub.AddExternalMapping(mapping);

        sub.ExternalMappings.Count.ShouldBe(1);
        sub.ExternalMappings[0].ProviderName.ShouldBe("stripe");
    }

    [Fact]
    public void AddExternalMapping_WithNull_ShouldThrow()
    {
        Subscription sub = CreateActiveSubscription();

        Should.Throw<ArgumentNullException>(() => sub.AddExternalMapping(null!));
    }

    // ======== Dunning ========

    [Fact]
    public void IncrementDunningAttempt_ShouldIncrement()
    {
        Subscription sub = CreateActiveSubscription();

        sub.IncrementDunningAttempt();
        sub.IncrementDunningAttempt();

        sub.DunningAttempt.ShouldBe(2);
    }

    [Fact]
    public void ResetDunning_ShouldResetToZero()
    {
        Subscription sub = CreateActiveSubscription();
        sub.IncrementDunningAttempt();
        sub.IncrementDunningAttempt();

        sub.ResetDunning();

        sub.DunningAttempt.ShouldBe(0);
    }

    // ======== Additional transitions ========

    [Fact]
    public void Activate_FromPastDue_ShouldReturnTrue()
    {
        Subscription sub = CreateActiveSubscription();
        sub.MarkPastDue();

        bool result = sub.Activate();

        result.ShouldBeTrue();
        sub.Status.ShouldBe(SubscriptionStatus.Active);
    }

    [Fact]
    public void Cancel_FromSuspended_ShouldReturnTrue()
    {
        Subscription sub = CreateActiveSubscription();
        sub.MarkPastDue();
        sub.Suspend();

        bool result = sub.Cancel("unpaid", DateTimeOffset.UtcNow);

        result.ShouldBeTrue();
        sub.Status.ShouldBe(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public void Suspend_FromActive_ShouldThrow()
    {
        Subscription sub = CreateActiveSubscription();

        Should.Throw<InvalidOperationException>(() => sub.Suspend());
    }
}
