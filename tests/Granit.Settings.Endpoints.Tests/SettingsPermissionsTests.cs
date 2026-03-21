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
        SettingsPermissions.Global.Read.ShouldBe("Settings.Global.Read");
        SettingsPermissions.Global.Read.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void GlobalManage_FollowsThreeSegmentFormat()
    {
        SettingsPermissions.Global.Manage.ShouldBe("Settings.Global.Manage");
        SettingsPermissions.Global.Manage.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void TenantRead_FollowsThreeSegmentFormat()
    {
        SettingsPermissions.Tenant.Read.ShouldBe("Settings.Tenant.Read");
        SettingsPermissions.Tenant.Read.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void TenantManage_FollowsThreeSegmentFormat()
    {
        SettingsPermissions.Tenant.Manage.ShouldBe("Settings.Tenant.Manage");
        SettingsPermissions.Tenant.Manage.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void AllPermissions_StartWithGroupName()
    {
        SettingsPermissions.Global.Read.ShouldStartWith(SettingsPermissions.GroupName + ".");
        SettingsPermissions.Global.Manage.ShouldStartWith(SettingsPermissions.GroupName + ".");
        SettingsPermissions.Tenant.Read.ShouldStartWith(SettingsPermissions.GroupName + ".");
        SettingsPermissions.Tenant.Manage.ShouldStartWith(SettingsPermissions.GroupName + ".");
    }
}
