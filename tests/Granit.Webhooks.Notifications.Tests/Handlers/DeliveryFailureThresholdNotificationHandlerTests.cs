using Granit.Notifications.Abstractions;
using Granit.Webhooks.Events;
using Granit.Webhooks.Notifications.Handlers;
using NSubstitute;
using Xunit;

namespace Granit.Webhooks.Notifications.Tests.Handlers;

public sealed class DeliveryFailureThresholdNotificationHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesNotification_ToSubscribers()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var subscriptionId = Guid.NewGuid();
        const string targetUrl = "https://example.com/webhooks/in";
        const int failureCount = 5;
        WebhookDeliveryFailureThresholdExceededEto evt = new(subscriptionId, targetUrl, failureCount);

        await DeliveryFailureThresholdNotificationHandler.HandleAsync(evt, publisher, CancellationToken.None);

        await publisher.Received(1).PublishToSubscribersAsync(
            WebhooksDeliveryFailureThresholdNotificationType.Instance,
            Arg.Is<WebhooksDeliveryFailureThresholdNotificationData>(d =>
                d.SubscriptionId == subscriptionId
                && d.TargetUrl == targetUrl
                && d.ConsecutiveFailureCount == failureCount),
            Arg.Any<CancellationToken>());
    }
}
