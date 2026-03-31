using Granit.Identity.Local.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Endpoints.Tests;

public sealed class IdentityLocalPermissionsTests
{
    [Fact]
    public void GroupName_IsIdentityLocal()
    {
        IdentityLocalPermissions.GroupName.ShouldBe("IdentityLocal");
    }

    [Fact]
    public void Impersonate_FollowsThreeSegmentFormat()
    {
        IdentityLocalPermissions.Users.Impersonate.ShouldBe("IdentityLocal.Users.Impersonate");
        IdentityLocalPermissions.Users.Impersonate.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void Impersonate_StartsWithGroupName()
    {
        IdentityLocalPermissions.Users.Impersonate.ShouldStartWith(IdentityLocalPermissions.GroupName + ".");
    }
}
