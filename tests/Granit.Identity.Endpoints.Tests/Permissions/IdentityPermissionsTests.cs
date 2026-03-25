using Granit.Identity.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests.Permissions;

public sealed class IdentityPermissionsTests
{
    [Fact]
    public void GroupName_IsIdentity() => IdentityPermissions.GroupName.ShouldBe("Identity");

    [Fact]
    public void Users_Read_FollowsThreeSegmentConvention() => IdentityPermissions.Users.Read.ShouldBe("Identity.Users.Read");

    [Fact]
    public void Users_Manage_FollowsThreeSegmentConvention() => IdentityPermissions.Users.Manage.ShouldBe("Identity.Users.Manage");

    [Fact]
    public void Users_Sync_FollowsThreeSegmentConvention() => IdentityPermissions.Users.Sync.ShouldBe("Identity.Users.Sync");

    [Fact]
    public void Users_Delete_FollowsThreeSegmentConvention() => IdentityPermissions.Users.Delete.ShouldBe("Identity.Users.Delete");

    [Fact]
    public void Roles_Read_FollowsThreeSegmentConvention() => IdentityPermissions.Roles.Read.ShouldBe("Identity.Roles.Read");

    [Fact]
    public void Roles_Manage_FollowsThreeSegmentConvention() => IdentityPermissions.Roles.Manage.ShouldBe("Identity.Roles.Manage");

    [Fact]
    public void Groups_Read_FollowsThreeSegmentConvention() => IdentityPermissions.Groups.Read.ShouldBe("Identity.Groups.Read");

    [Fact]
    public void Groups_Manage_FollowsThreeSegmentConvention() => IdentityPermissions.Groups.Manage.ShouldBe("Identity.Groups.Manage");

    [Fact]
    public void Sessions_Read_FollowsThreeSegmentConvention() => IdentityPermissions.Sessions.Read.ShouldBe("Identity.Sessions.Read");

    [Fact]
    public void Sessions_Manage_FollowsThreeSegmentConvention() => IdentityPermissions.Sessions.Manage.ShouldBe("Identity.Sessions.Manage");

    [Fact]
    public void Passwords_Manage_FollowsThreeSegmentConvention() => IdentityPermissions.Passwords.Manage.ShouldBe("Identity.Passwords.Manage");

    [Theory]
    [InlineData("Identity.Users.Read")]
    [InlineData("Identity.Users.Manage")]
    [InlineData("Identity.Users.Sync")]
    [InlineData("Identity.Users.Delete")]
    [InlineData("Identity.Roles.Read")]
    [InlineData("Identity.Roles.Manage")]
    [InlineData("Identity.Groups.Read")]
    [InlineData("Identity.Groups.Manage")]
    [InlineData("Identity.Sessions.Read")]
    [InlineData("Identity.Sessions.Manage")]
    [InlineData("Identity.Passwords.Manage")]
    public void AllPermissions_HaveThreeSegments(string permission)
    {
        string[] segments = permission.Split('.');
        segments.Length.ShouldBe(3);
    }
}
