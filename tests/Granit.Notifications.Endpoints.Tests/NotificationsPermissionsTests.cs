using Granit.Notifications.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Endpoints.Tests;

public sealed class NotificationsPermissionsTests
{
    [Fact]
    public void GroupName_IsNotifications() =>
        NotificationsPermissions.GroupName.ShouldBe("Notifications");

    [Fact]
    public void Notifications_Read_FollsThreeSegmentConvention() =>
        NotificationsPermissions.UserNotifications.Read.ShouldBe("Notifications.UserNotifications.Read");

    [Fact]
    public void Notifications_Manage_FollowsThreeSegmentConvention() =>
        NotificationsPermissions.UserNotifications.Manage.ShouldBe("Notifications.UserNotifications.Manage");

    [Fact]
    public void Read_StartsWithGroupName() =>
        NotificationsPermissions.UserNotifications.Read.ShouldStartWith(NotificationsPermissions.GroupName + ".");

    [Fact]
    public void Manage_StartsWithGroupName() =>
        NotificationsPermissions.UserNotifications.Manage.ShouldStartWith(NotificationsPermissions.GroupName + ".");

    [Fact]
    public void Read_HasThreeSegments() =>
        NotificationsPermissions.UserNotifications.Read.Split('.').Length.ShouldBe(3);

    [Fact]
    public void Manage_HasThreeSegments() =>
        NotificationsPermissions.UserNotifications.Manage.Split('.').Length.ShouldBe(3);
}
