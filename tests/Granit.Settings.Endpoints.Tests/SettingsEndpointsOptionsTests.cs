using Granit.Settings.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Settings.Endpoints.Tests;

public sealed class SettingsEndpointsOptionsTests
{
    [Fact]
    public void UserRoutePrefix_Default_IsSettingsUser()
    {
        SettingsEndpointsOptions options = new();

        options.UserRoutePrefix.ShouldBe("settings/user");
    }

    [Fact]
    public void GlobalRoutePrefix_Default_IsSettingsGlobal()
    {
        SettingsEndpointsOptions options = new();

        options.GlobalRoutePrefix.ShouldBe("settings/global");
    }

    [Fact]
    public void TenantRoutePrefix_Default_IsSettingsTenant()
    {
        SettingsEndpointsOptions options = new();

        options.TenantRoutePrefix.ShouldBe("settings/tenant");
    }

    [Fact]
    public void TagName_Default_IsSettings()
    {
        SettingsEndpointsOptions options = new();

        options.TagName.ShouldBe("Settings");
    }

    [Fact]
    public void AllProperties_CanBeCustomized()
    {
        SettingsEndpointsOptions options = new()
        {
            UserRoutePrefix = "custom/user",
            GlobalRoutePrefix = "custom/global",
            TenantRoutePrefix = "custom/tenant",
            TagName = "CustomTag",
        };

        options.UserRoutePrefix.ShouldBe("custom/user");
        options.GlobalRoutePrefix.ShouldBe("custom/global");
        options.TenantRoutePrefix.ShouldBe("custom/tenant");
        options.TagName.ShouldBe("CustomTag");
    }
}
