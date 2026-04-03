using Granit.Metering.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.Metering.Endpoints.Tests;

public sealed class MeteringPermissionsTests
{
    [Fact]
    public void GroupName_IsMetering() =>
        MeteringPermissions.GroupName.ShouldBe("Metering");

    [Fact]
    public void MetersRead_FollowsThreeSegmentFormat()
    {
        MeteringPermissions.Meters.Read.ShouldBe("Metering.Meters.Read");
        MeteringPermissions.Meters.Read.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void MetersManage_FollowsThreeSegmentFormat()
    {
        MeteringPermissions.Meters.Manage.ShouldBe("Metering.Meters.Manage");
        MeteringPermissions.Meters.Manage.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void UsageRead_FollowsThreeSegmentFormat()
    {
        MeteringPermissions.Usage.Read.ShouldBe("Metering.Usage.Read");
        MeteringPermissions.Usage.Read.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void UsageRecord_FollowsThreeSegmentFormat()
    {
        MeteringPermissions.Usage.Record.ShouldBe("Metering.Usage.Record");
        MeteringPermissions.Usage.Record.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void AllPermissions_StartWithGroupName()
    {
        string prefix = MeteringPermissions.GroupName + ".";

        MeteringPermissions.Meters.Read.ShouldStartWith(prefix);
        MeteringPermissions.Meters.Manage.ShouldStartWith(prefix);
        MeteringPermissions.Usage.Read.ShouldStartWith(prefix);
        MeteringPermissions.Usage.Record.ShouldStartWith(prefix);
    }
}
