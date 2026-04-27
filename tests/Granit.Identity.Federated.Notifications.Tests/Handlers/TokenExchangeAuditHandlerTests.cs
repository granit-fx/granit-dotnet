using Granit.Identity.Federated.Events;
using Granit.Identity.Federated.Notifications.Handlers;
using Granit.Notifications.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Notifications.Tests.Handlers;

public sealed class TokenExchangeAuditHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesTokenExchangeAuditNotification_ToSubscribers()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        const string targetUserId = "user-42";
        const string reason = "device-activity";
        DateTimeOffset occurredAt = new(2026, 4, 27, 10, 30, 0, TimeSpan.Zero);
        IdentityTokenExchangedEto evt = new(targetUserId, reason, occurredAt);

        await TokenExchangeAuditHandler.HandleAsync(evt, publisher, TestContext.Current.CancellationToken);

        await publisher.Received(1).PublishToSubscribersAsync(
            IdentityTokenExchangeAuditNotificationType.Instance,
            Arg.Is<IdentityTokenExchangeAuditNotificationData>(d =>
                d.TargetUserId == targetUserId
                && d.Reason == reason
                && d.OccurredAt == occurredAt),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NullEvent_Throws()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await TokenExchangeAuditHandler.HandleAsync(null!, publisher, CancellationToken.None));
    }
}
