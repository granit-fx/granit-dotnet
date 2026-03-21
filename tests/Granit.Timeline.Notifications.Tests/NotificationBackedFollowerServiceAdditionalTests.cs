using Granit.Core.MultiTenancy;
using Granit.Notifications.Abstractions;
using Granit.Timeline.Notifications.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Notifications.Tests;

public sealed class NotificationBackedFollowerServiceAdditionalTests
{
    private readonly INotificationSubscriptionReader _subscriptionReader = Substitute.For<INotificationSubscriptionReader>();
    private readonly INotificationSubscriptionWriter _subscriptionWriter = Substitute.For<INotificationSubscriptionWriter>();
    private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
    private readonly NotificationBackedFollowerService _service;

    public NotificationBackedFollowerServiceAdditionalTests()
    {
        _tenant.IsAvailable.Returns(false);
        _service = new NotificationBackedFollowerService(_subscriptionReader, _subscriptionWriter, _tenant);
    }

    [Fact]
    public async Task UnfollowAsync_WithTenant_PassesTenantId()
    {
        var tenantId = Guid.NewGuid();
        _tenant.IsAvailable.Returns(true);
        _tenant.Id.Returns(tenantId);

        await _service.UnfollowAsync("user-1", "Patient", "p-1", TestContext.Current.CancellationToken);

        await _subscriptionWriter.Received(1).UnfollowEntityAsync(
            "user-1", "Patient", "p-1", tenantId, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetFollowerIdsAsync_WithTenant_PassesTenantId()
    {
        var tenantId = Guid.NewGuid();
        _tenant.IsAvailable.Returns(true);
        _tenant.Id.Returns(tenantId);

        _subscriptionReader.GetEntityFollowerIdsAsync("Patient", "p-1", tenantId, TestContext.Current.CancellationToken)
            .Returns(["user-1"]);

        IReadOnlyList<string> result = await _service.GetFollowerIdsAsync(
            "Patient", "p-1", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
    }

    [Fact]
    public async Task IsFollowingAsync_WithTenant_PassesTenantId()
    {
        var tenantId = Guid.NewGuid();
        _tenant.IsAvailable.Returns(true);
        _tenant.Id.Returns(tenantId);

        _subscriptionReader.IsFollowingEntityAsync("user-1", "Patient", "p-1", tenantId, TestContext.Current.CancellationToken)
            .Returns(true);

        bool result = await _service.IsFollowingAsync(
            "user-1", "Patient", "p-1", TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsFollowingAsync_WhenNotFollowing_ReturnsFalse()
    {
        _subscriptionReader.IsFollowingEntityAsync("user-1", "Patient", "p-1", null, TestContext.Current.CancellationToken)
            .Returns(false);

        bool result = await _service.IsFollowingAsync(
            "user-1", "Patient", "p-1", TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }
}
