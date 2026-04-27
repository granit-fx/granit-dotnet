using Granit.Identity.Federated.Events;
using Granit.Identity.Federated.Notifications.Handlers;
using Granit.Notifications.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Notifications.Tests.Handlers;

public sealed class SyncFailedHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesSyncFailedNotification_ToSubscribers()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        const string userId = "user-42";
        const string providerName = "Keycloak";
        const string reason = "User not found in identity provider during cache sync.";
        DateTimeOffset occurredAt = new(2026, 4, 27, 10, 30, 0, TimeSpan.Zero);
        var tenantId = Guid.NewGuid();
        IdentityUserSyncFailedEto evt = new(userId, providerName, reason, occurredAt, tenantId);

        await SyncFailedHandler.HandleAsync(evt, publisher, TestContext.Current.CancellationToken);

        await publisher.Received(1).PublishToSubscribersAsync(
            IdentitySyncFailedNotificationType.Instance,
            Arg.Is<IdentitySyncFailedNotificationData>(d =>
                d.UserId == userId
                && d.ProviderName == providerName
                && d.Reason == reason
                && d.OccurredAt == occurredAt
                && d.TenantId == tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PassesThroughNullTenantId()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        IdentityUserSyncFailedEto evt = new(
            "user-7",
            "EntraId",
            "Provider returned 503.",
            DateTimeOffset.UtcNow,
            TenantId: null);

        await SyncFailedHandler.HandleAsync(evt, publisher, TestContext.Current.CancellationToken);

        await publisher.Received(1).PublishToSubscribersAsync(
            IdentitySyncFailedNotificationType.Instance,
            Arg.Is<IdentitySyncFailedNotificationData>(d => d.TenantId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NullEvent_Throws()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await SyncFailedHandler.HandleAsync(null!, publisher, CancellationToken.None));
    }
}
