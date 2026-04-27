using Granit.Identity.Federated.Events;
using Granit.Identity.Federated.Notifications.Handlers;
using Granit.Notifications.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Notifications.Tests.Handlers;

public sealed class UserProvisioningRemovedHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesUserProvisioningRemovedReceipt_ToSubscribers()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        DateTimeOffset now = new(2026, 4, 27, 11, 0, 0, TimeSpan.Zero);
        FakeTimeProvider time = new(now);
        const string userId = "user-deleted-7";
        var tenantId = Guid.NewGuid();
        IdentityUserDeletedEto evt = new(userId, tenantId);

        await UserProvisioningRemovedHandler.HandleAsync(evt, publisher, time, TestContext.Current.CancellationToken);

        await publisher.Received(1).PublishToSubscribersAsync(
            IdentityUserProvisioningRemovedNotificationType.Instance,
            Arg.Is<IdentityUserProvisioningRemovedNotificationData>(d =>
                d.UserId == userId
                && d.TenantId == tenantId
                && d.OccurredAt == now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_HostLevelDeletion_TenantIdIsNull()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        FakeTimeProvider time = new(now);
        IdentityUserDeletedEto evt = new("cross-tenant-user", TenantId: null);

        await UserProvisioningRemovedHandler.HandleAsync(evt, publisher, time, TestContext.Current.CancellationToken);

        await publisher.Received(1).PublishToSubscribersAsync(
            IdentityUserProvisioningRemovedNotificationType.Instance,
            Arg.Is<IdentityUserProvisioningRemovedNotificationData>(d =>
                d.UserId == "cross-tenant-user"
                && d.TenantId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NullEvent_Throws()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        FakeTimeProvider time = new();

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await UserProvisioningRemovedHandler.HandleAsync(null!, publisher, time, CancellationToken.None));
    }
}
