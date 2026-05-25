using Granit.Notifications;
using Granit.Notifications.Abstractions;

namespace Granit.Privacy.Notifications.Internal;

/// <summary>
/// Registers privacy notification definitions with their GDPR-aligned metadata
/// (group, opt-out posture, default channels). Most types are non-opt-outable
/// because they carry GDPR Art. 12 §3 / Art. 17 receipts.
/// </summary>
internal sealed class PrivacyNotificationDefinitionProvider : INotificationDefinitionProvider
{
    private const string GroupName = "Privacy";

    public void Define(INotificationDefinitionContext context)
    {
        context.Add(new NotificationDefinition(PrivacyExportReadyNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Personal Data Export Ready",
            Description = "Sent to the data subject when the personal data export archive is ready to download.",
            DefaultSeverity = NotificationSeverity.Success,
            DefaultChannels = [NotificationChannels.Email],
            AllowUserOptOut = false,
        });

        context.Add(new NotificationDefinition(PrivacyExportFailedNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Personal Data Export Failed",
            Description = "Sent to the data subject when the personal data export saga failed.",
            DefaultSeverity = NotificationSeverity.Warning,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = false,
        });

        context.Add(new NotificationDefinition(PrivacyDeletionReminderNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Account Deletion Reminder",
            Description = "Reminder sent before a deferred deletion request is executed, giving the data subject a final cancellation window.",
            DefaultSeverity = NotificationSeverity.Warning,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = false,
        });

        context.Add(new NotificationDefinition(PrivacyDeletionDeferredConfirmedNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Deletion Scheduled",
            Description = "Confirms that a deferred deletion request has been accepted and scheduled.",
            DefaultSeverity = NotificationSeverity.Info,
            DefaultChannels = [NotificationChannels.Email],
            AllowUserOptOut = false,
        });

        context.Add(new NotificationDefinition(PrivacyDeletionCancelledNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Deletion Cancelled",
            Description = "Confirms that a previously scheduled deletion request has been cancelled by the data subject.",
            DefaultSeverity = NotificationSeverity.Info,
            DefaultChannels = [NotificationChannels.Email],
            AllowUserOptOut = false,
        });

        context.Add(new NotificationDefinition(PrivacyDeletionAcknowledgedNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Deletion Acknowledged",
            Description = "Acknowledgement that a deletion request was received and is being processed (GDPR Art. 12 §3).",
            DefaultSeverity = NotificationSeverity.Info,
            DefaultChannels = [NotificationChannels.Email],
            AllowUserOptOut = false,
        });

        context.Add(new NotificationDefinition(PrivacyDeletionConfirmationNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Deletion Completed",
            Description = "Final receipt sent once the deletion request has been fully executed (GDPR Art. 17).",
            DefaultSeverity = NotificationSeverity.Info,
            DefaultChannels = [NotificationChannels.Email],
            AllowUserOptOut = false,
        });

        context.Add(new NotificationDefinition(PrivacyLegalDocumentObsoleteNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Legal Document Updated",
            Description = "Notifies the data subject that a legal document (privacy policy, terms of service) has been updated and requires acknowledgement.",
            DefaultSeverity = NotificationSeverity.Warning,
            DefaultChannels = [NotificationChannels.Email],
            AllowUserOptOut = false,
        });
    }
}
