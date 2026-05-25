using Granit.Identity.Federated.Notifications.Internal;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Notifications.Tests;

public sealed class NotificationTypeDefinitionDriftTests
{
    [Fact]
    public void All_notification_types_have_matching_definitions()
    {
        IdentityFederatedNotificationDefinitionProvider provider = new();
        CollectingContext context = new();
        provider.Define(context);

        string[] expectedNames =
        [
            IdentitySyncFailedNotificationType.Instance.Name,
            IdentityUserProvisioningRemovedNotificationType.Instance.Name,
            IdentityTokenExchangeAuditNotificationType.Instance.Name,
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
