using Granit.Notifications;
using Granit.Notifications.Abstractions;

namespace Granit.Webhooks.Notifications.Internal;

/// <summary>
/// Registers webhook notification definitions. All entries are non-opt-outable
/// because they are operational alerts for tenant administrators.
/// </summary>
internal sealed class WebhooksNotificationDefinitionProvider : INotificationDefinitionProvider
{
    private const string GroupName = "Webhooks";

    public void Define(INotificationDefinitionContext context)
    {
        context.Add(new NotificationDefinition(WebhooksDeliveryFailureThresholdNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Webhook Delivery Failure Threshold",
            Description = "Alerts tenant administrators when a webhook endpoint trips its delivery failure threshold.",
            DefaultSeverity = NotificationSeverity.Warning,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = false,
            RequiredPermission = "Webhooks.Subscriptions.Manage",
        });

        context.Add(new NotificationDefinition(WebhooksSigningKeyRotationDueNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Webhook Signing Key Rotation Due",
            Description = "Alerts tenant administrators when a webhook signing key is approaching its rotation deadline.",
            DefaultSeverity = NotificationSeverity.Warning,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = false,
            RequiredPermission = "Webhooks.Subscriptions.Manage",
        });
    }
}
