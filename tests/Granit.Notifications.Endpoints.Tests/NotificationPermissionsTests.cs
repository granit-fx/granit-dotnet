using Granit.Notifications.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Endpoints.Tests;

public sealed class NotificationPermissionsTests
{
    [Fact]
    public void GroupName_IsNotifications() =>
        NotificationPermissions.GroupName.ShouldBe("Notifications");

    [Fact]
    public void Notifications_Read_FollsThreeSegmentConvention() =>
        NotificationPermissions.Notifications.Read.ShouldBe("Notifications.Notifications.Read");

    [Fact]
    public void Notifications_Manage_FollowsThreeSegmentConvention() =>
        NotificationPermissions.Notifications.Manage.ShouldBe("Notifications.Notifications.Manage");

    [Fact]
    public void Read_StartsWithGroupName() =>
        NotificationPermissions.Notifications.Read.ShouldStartWith(NotificationPermissions.GroupName + ".");

    [Fact]
    public void Manage_StartsWithGroupName() =>
        NotificationPermissions.Notifications.Manage.ShouldStartWith(NotificationPermissions.GroupName + ".");

    [Fact]
    public void Read_HasThreeSegments() =>
        NotificationPermissions.Notifications.Read.Split('.').Length.ShouldBe(3);

    [Fact]
    public void Manage_HasThreeSegments() =>
        NotificationPermissions.Notifications.Manage.Split('.').Length.ShouldBe(3);
}
