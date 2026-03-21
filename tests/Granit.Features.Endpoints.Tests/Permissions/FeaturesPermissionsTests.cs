using Granit.Features.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.Features.Endpoints.Tests.Permissions;

public sealed class FeaturesPermissionsTests
{
    [Fact]
    public void GroupName_IsFeatures() => FeaturesPermissions.GroupName.ShouldBe("Features");

    [Fact]
    public void Flags_Read_FollowsThreeSegmentConvention() => FeaturesPermissions.Flags.Read.ShouldBe("Features.Flags.Read");

    [Fact]
    public void Flags_Manage_FollowsThreeSegmentConvention() => FeaturesPermissions.Flags.Manage.ShouldBe("Features.Flags.Manage");

    [Fact]
    public void Flags_Read_StartsWithGroupName() => FeaturesPermissions.Flags.Read.ShouldStartWith(FeaturesPermissions.GroupName + ".");

    [Fact]
    public void Flags_Manage_StartsWithGroupName() => FeaturesPermissions.Flags.Manage.ShouldStartWith(FeaturesPermissions.GroupName + ".");

    [Theory]
    [InlineData("Features.Flags.Read")]
    [InlineData("Features.Flags.Manage")]
    public void AllPermissions_HaveThreeDotSeparatedSegments(string permission)
    {
        string[] segments = permission.Split('.');
        segments.Length.ShouldBe(3);
    }
}
