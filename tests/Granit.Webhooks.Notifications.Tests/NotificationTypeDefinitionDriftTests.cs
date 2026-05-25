using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Granit.Webhooks.Notifications.Internal;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Notifications.Tests;

public sealed class NotificationTypeDefinitionDriftTests
{
    [Fact]
    public void All_notification_types_have_matching_definitions()
    {
        WebhooksNotificationDefinitionProvider provider = new();
        CollectingContext context = new();
        provider.Define(context);

        string[] expectedNames =
        [
            WebhooksDeliveryFailureThresholdNotificationType.Instance.Name,
            WebhooksSigningKeyRotationDueNotificationType.Instance.Name,
        ];

        foreach (string name in expectedNames)
        {
            context.Definitions.ShouldContain(d => d.Name == name,
                $"Missing NotificationDefinition for type '{name}'");
        }

        context.Definitions.Count.ShouldBe(expectedNames.Length);
    }

    private sealed class CollectingContext : INotificationDefinitionContext
    {
        public List<NotificationDefinition> Definitions { get; } = [];

        public void Add(NotificationDefinition definition) =>
            Definitions.Add(definition);
    }
}
