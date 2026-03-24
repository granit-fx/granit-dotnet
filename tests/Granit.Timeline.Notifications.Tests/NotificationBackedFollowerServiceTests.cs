// =============================================================================
// Tests — NotificationBackedFollowerService
// =============================================================================
// Verifies that the adapter correctly delegates to INotificationSubscriptionStore.
// =============================================================================

using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Granit.Timeline.Notifications.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Notifications.Tests;

public sealed class NotificationBackedFollowerServiceTests
{
    private readonly INotificationSubscriptionReader _subscriptionReader = Substitute.For<INotificationSubscriptionReader>();
    private readonly INotificationSubscriptionWriter _subscriptionWriter = Substitute.For<INotificationSubscriptionWriter>();
    private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
    private readonly NotificationBackedFollowerService _service;

    public NotificationBackedFollowerServiceTests()
    {
        _tenant.IsAvailable.Returns(false);
        _service = new NotificationBackedFollowerService(_subscriptionReader, _subscriptionWriter, _tenant);
    }

    [Fact]
    public async Task FollowAsync_DelegatesToSubscriptionStore()
    {
        await _service.FollowAsync("user-1", "Patient", "p-1", TestContext.Current.CancellationToken);

        await _subscriptionWriter.Received(1).FollowEntityAsync("user-1", "Patient", "p-1", null, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task FollowAsync_WithTenant_PassesTenantId()
    {
        var tenantId = Guid.NewGuid();
        _tenant.IsAvailable.Returns(true);
        _tenant.Id.Returns(tenantId);

        await _service.FollowAsync("user-1", "Patient", "p-1", TestContext.Current.CancellationToken);

        await _subscriptionWriter.Received(1).FollowEntityAsync("user-1", "Patient", "p-1", tenantId, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UnfollowAsync_DelegatesToSubscriptionStore()
    {
        await _service.UnfollowAsync("user-1", "Patient", "p-1", TestContext.Current.CancellationToken);

        await _subscriptionWriter.Received(1).UnfollowEntityAsync("user-1", "Patient", "p-1", null, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetFollowerIdsAsync_DelegatesToSubscriptionStore()
    {
        _subscriptionReader.GetEntityFollowerIdsAsync("Patient", "p-1", null, TestContext.Current.CancellationToken)
            .Returns(["user-1", "user-2"]);

        IReadOnlyList<string> result = await _service.GetFollowerIdsAsync("Patient", "p-1", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result.ShouldContain("user-1");
        result.ShouldContain("user-2");
    }

    [Fact]
    public async Task IsFollowingAsync_DelegatesToSubscriptionStore()
    {
        _subscriptionReader.IsFollowingEntityAsync("user-1", "Patient", "p-1", null, TestContext.Current.CancellationToken)
            .Returns(true);

        bool result = await _service.IsFollowingAsync("user-1", "Patient", "p-1", TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
    }
}
