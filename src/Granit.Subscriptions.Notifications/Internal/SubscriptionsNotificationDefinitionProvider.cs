using Granit.Notifications;
using Granit.Notifications.Abstractions;

namespace Granit.Subscriptions.Notifications.Internal;

internal sealed class SubscriptionsNotificationDefinitionProvider : INotificationDefinitionProvider
{
    private const string GroupName = "Subscriptions";

    public void Define(INotificationDefinitionContext context)
    {
        context.Add(new NotificationDefinition(TrialExpiringNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Trial Expiring",
            Description = "Warning sent before a trial period ends.",
            DefaultSeverity = NotificationSeverity.Warning,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = true,
        });

        context.Add(new NotificationDefinition(TrialExpiredNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Trial Expired",
            Description = "Notification when a trial has ended.",
            DefaultSeverity = NotificationSeverity.Info,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = false,
        });

        context.Add(new NotificationDefinition(PlanChangedNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Plan Changed",
            Description = "Confirmation of a subscription plan upgrade or downgrade.",
            DefaultSeverity = NotificationSeverity.Info,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = true,
        });

        context.Add(new NotificationDefinition(CancellationConfirmedNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Cancellation Confirmed",
            Description = "Confirmation that a subscription has been cancelled.",
            DefaultSeverity = NotificationSeverity.Info,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = false,
        });

        context.Add(new NotificationDefinition(SuspensionWarningNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Subscription Suspended",
            Description = "Alert when a subscription is suspended due to unpaid invoices.",
            DefaultSeverity = NotificationSeverity.Error,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = false,
        });

        context.Add(new NotificationDefinition(ScheduledChangeReminderNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Scheduled Change Reminder",
            Description = "Reminder of an upcoming scheduled plan change.",
            DefaultSeverity = NotificationSeverity.Info,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = true,
        });
    }
}
