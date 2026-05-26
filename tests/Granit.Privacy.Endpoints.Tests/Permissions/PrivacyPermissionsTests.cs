using Granit.Privacy.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Endpoints.Tests.Permissions;

public sealed class PrivacyPermissionsTests
{
    [Fact]
    public void GroupName_IsPrivacy() => PrivacyPermissions.GroupName.ShouldBe("Privacy");

    [Fact]
    public void Export_Execute_FollowsThreeSegmentConvention() =>
        PrivacyPermissions.Exports.Execute.ShouldBe("Privacy.Exports.Execute");

    [Fact]
    public void Deletion_Execute_FollowsThreeSegmentConvention() =>
        PrivacyPermissions.Deletions.Execute.ShouldBe("Privacy.Deletions.Execute");

    [Fact]
    public void Exports_OnBehalfOf_FollowsThreeSegmentConvention() =>
        PrivacyPermissions.Exports.OnBehalfOf.ShouldBe("Privacy.Exports.OnBehalfOf");

    [Fact]
    public void Agreements_Read_FollowsThreeSegmentConvention() =>
        PrivacyPermissions.Agreements.Read.ShouldBe("Privacy.Agreements.Read");

    [Fact]
    public void Agreements_Create_FollowsThreeSegmentConvention() =>
        PrivacyPermissions.Agreements.Create.ShouldBe("Privacy.Agreements.Create");

    [Theory]
    [InlineData("Privacy.Exports.Execute")]
    [InlineData("Privacy.Exports.OnBehalfOf")]
    [InlineData("Privacy.Deletions.Execute")]
    [InlineData("Privacy.Agreements.Read")]
    [InlineData("Privacy.Agreements.Create")]
    public void AllPermissions_HaveThreeDotSeparatedSegments(string permission)
    {
        string[] segments = permission.Split('.');
        segments.Length.ShouldBe(3);
    }

    [Theory]
    [InlineData("Privacy.Exports.Execute")]
    [InlineData("Privacy.Exports.OnBehalfOf")]
    [InlineData("Privacy.Deletions.Execute")]
    [InlineData("Privacy.Agreements.Read")]
    [InlineData("Privacy.Agreements.Create")]
    public void AllPermissions_StartWithGroupName(string permission) =>
        permission.ShouldStartWith(PrivacyPermissions.GroupName + ".");
}
