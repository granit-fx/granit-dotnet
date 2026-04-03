using Granit.Subscriptions.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.Endpoints.Tests;

public sealed class SubscriptionsPermissionsTests
{
    [Fact]
    public void GroupName_IsSubscriptions() =>
        SubscriptionsPermissions.GroupName.ShouldBe("Subscriptions");

    [Fact]
    public void PlansRead_FollowsThreeSegmentFormat()
    {
        SubscriptionsPermissions.Plans.Read.ShouldBe("Subscriptions.Plans.Read");
        SubscriptionsPermissions.Plans.Read.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void PlansManage_FollowsThreeSegmentFormat()
    {
        SubscriptionsPermissions.Plans.Manage.ShouldBe("Subscriptions.Plans.Manage");
        SubscriptionsPermissions.Plans.Manage.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void SubscriptionsRead_FollowsThreeSegmentFormat()
    {
        SubscriptionsPermissions.Subscriptions.Read.ShouldBe("Subscriptions.Subscriptions.Read");
        SubscriptionsPermissions.Subscriptions.Read.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void SubscriptionsManage_FollowsThreeSegmentFormat()
    {
        SubscriptionsPermissions.Subscriptions.Manage.ShouldBe("Subscriptions.Subscriptions.Manage");
        SubscriptionsPermissions.Subscriptions.Manage.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void SeatsRead_FollowsThreeSegmentFormat()
    {
        SubscriptionsPermissions.Seats.Read.ShouldBe("Subscriptions.Seats.Read");
        SubscriptionsPermissions.Seats.Read.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void SeatsManage_FollowsThreeSegmentFormat()
    {
        SubscriptionsPermissions.Seats.Manage.ShouldBe("Subscriptions.Seats.Manage");
        SubscriptionsPermissions.Seats.Manage.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void AllPermissions_StartWithGroupName()
    {
        string prefix = SubscriptionsPermissions.GroupName + ".";

        SubscriptionsPermissions.Plans.Read.ShouldStartWith(prefix);
        SubscriptionsPermissions.Plans.Manage.ShouldStartWith(prefix);
        SubscriptionsPermissions.Subscriptions.Read.ShouldStartWith(prefix);
        SubscriptionsPermissions.Subscriptions.Manage.ShouldStartWith(prefix);
        SubscriptionsPermissions.Seats.Read.ShouldStartWith(prefix);
        SubscriptionsPermissions.Seats.Manage.ShouldStartWith(prefix);
    }
}
