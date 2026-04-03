using Granit.Notifications;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.Notifications.Tests;

public sealed class SubscriptionsNotificationTypeTests
{
    [Fact]
    public void TrialExpiring_ShouldHaveCorrectMetadata()
    {
        TrialExpiringNotificationType type = TrialExpiringNotificationType.Instance;

        type.Name.ShouldBe("Subscriptions.TrialExpiring");
        type.DefaultSeverity.ShouldBe(NotificationSeverity.Warning);
        type.DefaultChannels.ShouldContain(NotificationChannels.Email);
        type.DefaultChannels.ShouldContain(NotificationChannels.InApp);
        type.DefaultChannels.Count.ShouldBe(2);
    }

    [Fact]
    public void TrialExpiring_ShouldBeSingleton()
    {
        TrialExpiringNotificationType.Instance.ShouldBeSameAs(TrialExpiringNotificationType.Instance);
    }

    [Fact]
    public void TrialExpiringData_ShouldCreateRecord()
    {
        var subscriptionId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        DateTimeOffset trialEndsAt = DateTimeOffset.UtcNow.AddDays(3);

        var data = new TrialExpiringNotificationData(subscriptionId, planId, "Pro", 3, trialEndsAt);

        data.SubscriptionId.ShouldBe(subscriptionId);
        data.PlanId.ShouldBe(planId);
        data.PlanName.ShouldBe("Pro");
        data.DaysRemaining.ShouldBe(3);
        data.TrialEndsAt.ShouldBe(trialEndsAt);
    }

    [Fact]
    public void TrialExpired_ShouldHaveCorrectMetadata()
    {
        TrialExpiredNotificationType type = TrialExpiredNotificationType.Instance;

        type.Name.ShouldBe("Subscriptions.TrialExpired");
        type.DefaultSeverity.ShouldBe(NotificationSeverity.Info);
        type.DefaultChannels.ShouldContain(NotificationChannels.Email);
        type.DefaultChannels.ShouldContain(NotificationChannels.InApp);
        type.DefaultChannels.Count.ShouldBe(2);
    }

    [Fact]
    public void TrialExpired_ShouldBeSingleton()
    {
        TrialExpiredNotificationType.Instance.ShouldBeSameAs(TrialExpiredNotificationType.Instance);
    }

    [Fact]
    public void TrialExpiredData_ShouldCreateRecord()
    {
        var subscriptionId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        var data = new TrialExpiredNotificationData(subscriptionId, planId, "Starter");

        data.SubscriptionId.ShouldBe(subscriptionId);
        data.PlanId.ShouldBe(planId);
        data.PlanName.ShouldBe("Starter");
    }

    [Fact]
    public void PlanChanged_ShouldHaveCorrectMetadata()
    {
        PlanChangedNotificationType type = PlanChangedNotificationType.Instance;

        type.Name.ShouldBe("Subscriptions.PlanChanged");
        type.DefaultSeverity.ShouldBe(NotificationSeverity.Info);
        type.DefaultChannels.ShouldContain(NotificationChannels.Email);
        type.DefaultChannels.ShouldContain(NotificationChannels.InApp);
        type.DefaultChannels.Count.ShouldBe(2);
    }

    [Fact]
    public void PlanChanged_ShouldBeSingleton()
    {
        PlanChangedNotificationType.Instance.ShouldBeSameAs(PlanChangedNotificationType.Instance);
    }

    [Fact]
    public void PlanChangedData_ShouldCreateRecord()
    {
        var subscriptionId = Guid.NewGuid();
        var previousPlanId = Guid.NewGuid();
        var newPlanId = Guid.NewGuid();

        var data = new PlanChangedNotificationData(
            subscriptionId, previousPlanId, "Starter", newPlanId, "Pro");

        data.SubscriptionId.ShouldBe(subscriptionId);
        data.PreviousPlanId.ShouldBe(previousPlanId);
        data.PreviousPlanName.ShouldBe("Starter");
        data.NewPlanId.ShouldBe(newPlanId);
        data.NewPlanName.ShouldBe("Pro");
    }

    [Fact]
    public void CancellationConfirmed_ShouldHaveCorrectMetadata()
    {
        CancellationConfirmedNotificationType type = CancellationConfirmedNotificationType.Instance;

        type.Name.ShouldBe("Subscriptions.CancellationConfirmed");
        type.DefaultSeverity.ShouldBe(NotificationSeverity.Info);
        type.DefaultChannels.ShouldContain(NotificationChannels.Email);
        type.DefaultChannels.ShouldContain(NotificationChannels.InApp);
        type.DefaultChannels.Count.ShouldBe(2);
    }

    [Fact]
    public void CancellationConfirmed_ShouldBeSingleton()
    {
        CancellationConfirmedNotificationType.Instance
            .ShouldBeSameAs(CancellationConfirmedNotificationType.Instance);
    }

    [Fact]
    public void CancellationConfirmedData_ShouldCreateRecord()
    {
        var subscriptionId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        DateTimeOffset cancelledAt = DateTimeOffset.UtcNow;

        var data = new CancellationConfirmedNotificationData(
            subscriptionId, planId, "Pro", "Too expensive", cancelledAt);

        data.SubscriptionId.ShouldBe(subscriptionId);
        data.PlanId.ShouldBe(planId);
        data.PlanName.ShouldBe("Pro");
        data.CancellationReason.ShouldBe("Too expensive");
        data.CancelledAt.ShouldBe(cancelledAt);
    }

    [Fact]
    public void CancellationConfirmedData_ShouldAllowNullReason()
    {
        var data = new CancellationConfirmedNotificationData(
            Guid.NewGuid(), Guid.NewGuid(), "Pro", null, DateTimeOffset.UtcNow);

        data.CancellationReason.ShouldBeNull();
    }

    [Fact]
    public void SuspensionWarning_ShouldHaveCorrectMetadata()
    {
        SuspensionWarningNotificationType type = SuspensionWarningNotificationType.Instance;

        type.Name.ShouldBe("Subscriptions.SuspensionWarning");
        type.DefaultSeverity.ShouldBe(NotificationSeverity.Error);
        type.DefaultChannels.ShouldContain(NotificationChannels.Email);
        type.DefaultChannels.ShouldContain(NotificationChannels.InApp);
        type.DefaultChannels.Count.ShouldBe(2);
    }

    [Fact]
    public void SuspensionWarning_ShouldBeSingleton()
    {
        SuspensionWarningNotificationType.Instance
            .ShouldBeSameAs(SuspensionWarningNotificationType.Instance);
    }

    [Fact]
    public void SuspensionWarningData_ShouldCreateRecord()
    {
        var subscriptionId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        var data = new SuspensionWarningNotificationData(subscriptionId, planId, "Enterprise");

        data.SubscriptionId.ShouldBe(subscriptionId);
        data.PlanId.ShouldBe(planId);
        data.PlanName.ShouldBe("Enterprise");
    }

    [Fact]
    public void ScheduledChangeReminder_ShouldHaveCorrectMetadata()
    {
        ScheduledChangeReminderNotificationType type = ScheduledChangeReminderNotificationType.Instance;

        type.Name.ShouldBe("Subscriptions.ScheduledChangeReminder");
        type.DefaultSeverity.ShouldBe(NotificationSeverity.Info);
        type.DefaultChannels.ShouldContain(NotificationChannels.Email);
        type.DefaultChannels.ShouldContain(NotificationChannels.InApp);
        type.DefaultChannels.Count.ShouldBe(2);
    }

    [Fact]
    public void ScheduledChangeReminder_ShouldBeSingleton()
    {
        ScheduledChangeReminderNotificationType.Instance
            .ShouldBeSameAs(ScheduledChangeReminderNotificationType.Instance);
    }

    [Fact]
    public void ScheduledChangeReminderData_ShouldCreateRecord()
    {
        var subscriptionId = Guid.NewGuid();
        var currentPlanId = Guid.NewGuid();
        var newPlanId = Guid.NewGuid();
        DateTimeOffset scheduledAt = DateTimeOffset.UtcNow.AddDays(7);

        var data = new ScheduledChangeReminderNotificationData(
            subscriptionId, currentPlanId, "Starter", newPlanId, "Pro", scheduledAt);

        data.SubscriptionId.ShouldBe(subscriptionId);
        data.CurrentPlanId.ShouldBe(currentPlanId);
        data.CurrentPlanName.ShouldBe("Starter");
        data.NewPlanId.ShouldBe(newPlanId);
        data.NewPlanName.ShouldBe("Pro");
        data.ScheduledAt.ShouldBe(scheduledAt);
    }
}
