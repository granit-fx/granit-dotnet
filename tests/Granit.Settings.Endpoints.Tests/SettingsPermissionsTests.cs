using Granit.Settings.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.Settings.Endpoints.Tests;

public sealed class SettingsPermissionsTests
{
    [Fact]
    public void GroupName_IsSettings() => SettingsPermissions.GroupName.ShouldBe("Settings");

    [Fact]
    public void GlobalRead_FollowsThreeSegmentFormat()
    {
        SettingsPermissions.GlobalRead.ShouldBe("Settings.Global.Read");
        SettingsPermissions.GlobalRead.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void GlobalManage_FollowsThreeSegmentFormat()
    {
        SettingsPermissions.GlobalManage.ShouldBe("Settings.Global.Manage");
        SettingsPermissions.GlobalManage.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void TenantRead_FollowsThreeSegmentFormat()
    {
        SettingsPermissions.TenantRead.ShouldBe("Settings.Tenant.Read");
        SettingsPermissions.TenantRead.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void TenantManage_FollowsThreeSegmentFormat()
    {
        SettingsPermissions.TenantManage.ShouldBe("Settings.Tenant.Manage");
        SettingsPermissions.TenantManage.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void AllPermissions_StartWithGroupName()
    {
        SettingsPermissions.GlobalRead.ShouldStartWith(SettingsPermissions.GroupName + ".");
        SettingsPermissions.GlobalManage.ShouldStartWith(SettingsPermissions.GroupName + ".");
        SettingsPermissions.TenantRead.ShouldStartWith(SettingsPermissions.GroupName + ".");
        SettingsPermissions.TenantManage.ShouldStartWith(SettingsPermissions.GroupName + ".");
    }
}
