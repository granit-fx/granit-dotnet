using Granit.Scheduling.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.Scheduling.Endpoints.Tests;

public sealed class SchedulingPermissionsTests
{
    [Fact]
    public void Permissions_ShouldFollowThreeSegmentNaming()
    {
        SchedulingPermissions.Actions.Read.ShouldBe("Scheduling.Actions.Read");
        SchedulingPermissions.Actions.Manage.ShouldBe("Scheduling.Actions.Manage");
    }

    [Theory]
    [InlineData(nameof(SchedulingPermissions.Actions.Read))]
    [InlineData(nameof(SchedulingPermissions.Actions.Manage))]
    public void Permissions_ShouldHaveThreeDotSeparatedSegments(string permissionName)
    {
        string value = permissionName switch
        {
            nameof(SchedulingPermissions.Actions.Read) => SchedulingPermissions.Actions.Read,
            nameof(SchedulingPermissions.Actions.Manage) => SchedulingPermissions.Actions.Manage,
            _ => throw new ArgumentOutOfRangeException(nameof(permissionName)),
        };

        value.Split('.').Length.ShouldBe(3);
    }
}
