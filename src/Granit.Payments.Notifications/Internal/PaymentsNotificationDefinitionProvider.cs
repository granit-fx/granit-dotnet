using Granit.Notifications;
using Granit.Notifications.Abstractions;

namespace Granit.Payments.Notifications.Internal;

internal sealed class PaymentsNotificationDefinitionProvider : INotificationDefinitionProvider
{
    private const string GroupName = "Payments";

    public void Define(INotificationDefinitionContext context)
    {
        context.Add(new NotificationDefinition(PaymentSucceededNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Payment Succeeded",
            Description = "Confirmation when a payment is successfully processed.",
            DefaultSeverity = NotificationSeverity.Success,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = true,
        });

        context.Add(new NotificationDefinition(PaymentFailedNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Payment Failed",
            Description = "Alert when a payment attempt fails.",
            DefaultSeverity = NotificationSeverity.Warning,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = false,
        });

        context.Add(new NotificationDefinition(PaymentMethodExpiringNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Payment Method Expiring",
            Description = "Warning when a saved payment method is about to expire.",
            DefaultSeverity = NotificationSeverity.Warning,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = true,
        });

        context.Add(new NotificationDefinition(RefundProcessedNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Refund Processed",
            Description = "Confirmation when a refund is successfully processed.",
            DefaultSeverity = NotificationSeverity.Success,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = true,
        });

        context.Add(new NotificationDefinition(DisputeOpenedNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Dispute Opened",
            Description = "Alert when a payment dispute (chargeback) is opened.",
            DefaultSeverity = NotificationSeverity.Error,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = false,
        });
    }
}
