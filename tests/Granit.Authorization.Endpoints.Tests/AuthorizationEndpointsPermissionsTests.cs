using Granit.Authorization.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Endpoints.Tests;

public sealed class AuthorizationEndpointsPermissionsTests
{
    [Fact]
    public void GroupName_IsAuthorization() => AuthorizationEndpointsPermissions.GroupName.ShouldBe("Authorization");

    [Fact]
    public void Definitions_Read_FollowsThreeSegmentConvention() => AuthorizationEndpointsPermissions.Definitions.Read.ShouldBe("Authorization.Definitions.Read");

    [Fact]
    public void Grants_Manage_FollowsThreeSegmentConvention() => AuthorizationEndpointsPermissions.Grants.Manage.ShouldBe("Authorization.Grants.Manage");

    [Fact]
    public void Definitions_Read_StartsWithGroupName()
    {
        AuthorizationEndpointsPermissions.Definitions.Read
            .ShouldStartWith(AuthorizationEndpointsPermissions.GroupName + ".");
    }

    [Fact]
    public void Grants_Manage_StartsWithGroupName()
    {
        AuthorizationEndpointsPermissions.Grants.Manage
            .ShouldStartWith(AuthorizationEndpointsPermissions.GroupName + ".");
    }
}
