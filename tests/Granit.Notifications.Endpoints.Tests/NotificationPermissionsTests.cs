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
        NotificationPermissions.UserNotifications.Read.ShouldBe("Notifications.UserNotifications.Read");

    [Fact]
    public void Notifications_Manage_FollowsThreeSegmentConvention() =>
        NotificationPermissions.UserNotifications.Manage.ShouldBe("Notifications.UserNotifications.Manage");

    [Fact]
    public void Read_StartsWithGroupName() =>
        NotificationPermissions.UserNotifications.Read.ShouldStartWith(NotificationPermissions.GroupName + ".");

    [Fact]
    public void Manage_StartsWithGroupName() =>
        NotificationPermissions.UserNotifications.Manage.ShouldStartWith(NotificationPermissions.GroupName + ".");

    [Fact]
    public void Read_HasThreeSegments() =>
        NotificationPermissions.UserNotifications.Read.Split('.').Length.ShouldBe(3);

    [Fact]
    public void Manage_HasThreeSegments() =>
        NotificationPermissions.UserNotifications.Manage.Split('.').Length.ShouldBe(3);
}
