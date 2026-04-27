using Granit.Notifications.Abstractions;
using Granit.Webhooks.Events;
using Granit.Webhooks.Notifications.Handlers;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Granit.Webhooks.Notifications.Tests.Handlers;

public sealed class SigningKeyRotationDueHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesNotification_WithDaysUntilExpiry()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var now = new DateTimeOffset(2026, 4, 27, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);

        var subscriptionId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        // 10 days, 6h ahead → ceiling = 11 days
        DateTimeOffset expiresAt = now.AddDays(10).AddHours(6);

        var evt = new WebhookSigningKeyRotationDueEto(subscriptionId, keyId, expiresAt);

        await SigningKeyRotationDueHandler.HandleAsync(evt, publisher, timeProvider, CancellationToken.None);

        await publisher.Received(1).PublishToSubscribersAsync(
            WebhooksSigningKeyRotationDueNotificationType.Instance,
            Arg.Is<WebhooksSigningKeyRotationDueNotificationData>(d =>
                d.SubscriptionId == subscriptionId
                && d.KeyId == keyId
                && d.ExpiresAt == expiresAt
                && d.DaysUntilExpiry == 11),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_KeyAlreadyExpired_ClampsDaysUntilExpiryToZero()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var now = new DateTimeOffset(2026, 4, 27, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);

        var evt = new WebhookSigningKeyRotationDueEto(
            Guid.NewGuid(), Guid.NewGuid(), now.AddDays(-2));

        await SigningKeyRotationDueHandler.HandleAsync(evt, publisher, timeProvider, CancellationToken.None);

        await publisher.Received(1).PublishToSubscribersAsync(
            WebhooksSigningKeyRotationDueNotificationType.Instance,
            Arg.Is<WebhooksSigningKeyRotationDueNotificationData>(d => d.DaysUntilExpiry == 0),
            Arg.Any<CancellationToken>());
    }
}
