using Granit.Identity.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests.Permissions;

public sealed class IdentityUserCachePermissionsTests
{
    [Fact]
    public void GroupName_IsIdentity() => IdentityUserCachePermissions.GroupName.ShouldBe("Identity");

    [Fact]
    public void UserCache_Read_FollowsThreeSegmentConvention() => IdentityUserCachePermissions.UserCache.Read.ShouldBe("Identity.UserCache.Read");

    [Fact]
    public void UserCache_Sync_FollowsThreeSegmentConvention() => IdentityUserCachePermissions.UserCache.Sync.ShouldBe("Identity.UserCache.Sync");

    [Fact]
    public void UserCache_Delete_FollowsThreeSegmentConvention() => IdentityUserCachePermissions.UserCache.Delete.ShouldBe("Identity.UserCache.Delete");

    [Theory]
    [InlineData("Identity.UserCache.Read")]
    [InlineData("Identity.UserCache.Sync")]
    [InlineData("Identity.UserCache.Delete")]
    public void AllPermissions_HaveThreeSegments(string permission)
    {
        string[] segments = permission.Split('.');
        segments.Length.ShouldBe(3);
    }
}
