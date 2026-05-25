using Granit.Notifications;
using Granit.Notifications.Abstractions;

namespace Granit.Authentication.ApiKeys.Notifications.Internal;

/// <summary>
/// Registers API key lifecycle notification definitions. All entries are
/// non-opt-outable (security alerts, ISO 27001 A.9.4).
/// </summary>
internal sealed class ApiKeysNotificationDefinitionProvider : INotificationDefinitionProvider
{
    private const string GroupName = "Security";

    public void Define(INotificationDefinitionContext context)
    {
        context.Add(new NotificationDefinition(ApiKeyIssuedNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "API Key Issued",
            Description = "Sent when a new API key is issued, with the public-safe metadata (id, name, prefix).",
            DefaultSeverity = NotificationSeverity.Info,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = false,
        });

        context.Add(new NotificationDefinition(ApiKeyRotatedNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "API Key Rotated",
            Description = "Sent when an API key is rotated, signalling that the prior secret is invalidated.",
            DefaultSeverity = NotificationSeverity.Info,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = false,
        });

        context.Add(new NotificationDefinition(ApiKeyRevokedNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "API Key Revoked",
            Description = "Sent when an API key is revoked outside the normal rotation flow.",
            DefaultSeverity = NotificationSeverity.Info,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = false,
        });

        context.Add(new NotificationDefinition(ApiKeyExpiringSoonNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "API Key Expiring Soon",
            Description = "Warns the owner that an API key is approaching its expiration date so it can be rotated before service disruption.",
            DefaultSeverity = NotificationSeverity.Warning,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = false,
        });
    }
}
