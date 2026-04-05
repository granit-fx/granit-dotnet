using Granit.MultiTenancy.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Endpoints.Tests.Permissions;

public sealed class MultiTenancyPermissionsTests
{
    [Fact]
    public void GroupName_IsMultiTenancy() =>
        MultiTenancyPermissions.GroupName.ShouldBe("MultiTenancy");

    [Fact]
    public void TenantsRead_FollowsThreeSegmentFormat()
    {
        MultiTenancyPermissions.Tenants.Read.ShouldBe("MultiTenancy.Tenants.Read");
        MultiTenancyPermissions.Tenants.Read.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void TenantsCreate_FollowsThreeSegmentFormat()
    {
        MultiTenancyPermissions.Tenants.Create.ShouldBe("MultiTenancy.Tenants.Create");
        MultiTenancyPermissions.Tenants.Create.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void TenantsUpdate_FollowsThreeSegmentFormat()
    {
        MultiTenancyPermissions.Tenants.Update.ShouldBe("MultiTenancy.Tenants.Update");
        MultiTenancyPermissions.Tenants.Update.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void TenantsManage_FollowsThreeSegmentFormat()
    {
        MultiTenancyPermissions.Tenants.Manage.ShouldBe("MultiTenancy.Tenants.Manage");
        MultiTenancyPermissions.Tenants.Manage.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void AllPermissions_StartWithGroupName()
    {
        MultiTenancyPermissions.Tenants.Read.ShouldStartWith(MultiTenancyPermissions.GroupName + ".");
        MultiTenancyPermissions.Tenants.Create.ShouldStartWith(MultiTenancyPermissions.GroupName + ".");
        MultiTenancyPermissions.Tenants.Update.ShouldStartWith(MultiTenancyPermissions.GroupName + ".");
        MultiTenancyPermissions.Tenants.Manage.ShouldStartWith(MultiTenancyPermissions.GroupName + ".");
    }
}
