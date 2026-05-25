using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Granit.Privacy.Notifications.Internal;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Notifications.Tests;

public sealed class NotificationTypeDefinitionDriftTests
{
    [Fact]
    public void All_notification_types_have_matching_definitions()
    {
        PrivacyNotificationDefinitionProvider provider = new();
        CollectingContext context = new();
        provider.Define(context);

        string[] expectedNames =
        [
            PrivacyExportReadyNotificationType.Instance.Name,
            PrivacyExportFailedNotificationType.Instance.Name,
            PrivacyDeletionReminderNotificationType.Instance.Name,
            PrivacyDeletionDeferredConfirmedNotificationType.Instance.Name,
            PrivacyDeletionCancelledNotificationType.Instance.Name,
            PrivacyDeletionAcknowledgedNotificationType.Instance.Name,
            PrivacyDeletionConfirmationNotificationType.Instance.Name,
            PrivacyLegalDocumentObsoleteNotificationType.Instance.Name,
        ];

        foreach (string name in expectedNames)
        {
            context.Definitions.ShouldContain(d => d.Name == name,
                $"Missing NotificationDefinition for type '{name}'");
        }

        context.Definitions.Count.ShouldBe(expectedNames.Length,
            "Definition count should match the number of notification types");
    }

    private sealed class CollectingContext : INotificationDefinitionContext
    {
        public List<NotificationDefinition> Definitions { get; } = [];

        public void Add(NotificationDefinition definition) =>
            Definitions.Add(definition);
    }
}
