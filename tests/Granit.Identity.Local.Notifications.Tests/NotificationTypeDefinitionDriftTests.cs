using Granit.Identity.Local.Notifications.Internal;
using Granit.Identity.Local.Notifications.NotificationTypes;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Notifications.Tests;

public sealed class NotificationTypeDefinitionDriftTests
{
    [Fact]
    public void All_notification_types_have_matching_definitions()
    {
        // Arrange
        IdentityNotificationDefinitionProvider provider = new();
        CollectingContext context = new();
        provider.Define(context);

        // All 9 notification type names
        string[] expectedNames =
        [
            WelcomeNotificationType.Instance.Name,
            PasswordResetNotificationType.Instance.Name,
            ImpersonationAlertNotificationType.Instance.Name,
            EmailConfirmationNotificationType.Instance.Name,
            PasswordChangedNotificationType.Instance.Name,
            AccountLockedNotificationType.Instance.Name,
            TwoFactorChangedNotificationType.Instance.Name,
            EmailChangeAlertNotificationType.Instance.Name,
            EmailChangeConfirmationNotificationType.Instance.Name,
        ];

        foreach (string name in expectedNames)
        {
            context.Definitions.ShouldContain(d => d.Name == name,
                $"Missing NotificationDefinition for type '{name}'");
        }

        context.Definitions.Count.ShouldBe(expectedNames.Length,
            "Definition count should match the number of notification types");
    }

    /// <summary>
    /// Lightweight test-local implementation of <see cref="INotificationDefinitionContext"/>
    /// that collects definitions for assertion.
    /// </summary>
    private sealed class CollectingContext : INotificationDefinitionContext
    {
        public List<NotificationDefinition> Definitions { get; } = [];

        public void Add(NotificationDefinition definition) =>
            Definitions.Add(definition);
    }
}
