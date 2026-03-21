using Granit.Localization.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.Localization.Endpoints.Tests;

public sealed class LocalizationOverridesPermissionsTests
{
    [Fact]
    public void GroupName_IsLocalization() => LocalizationOverridesPermissions.GroupName.ShouldBe("Localization");

    [Fact]
    public void Read_FollowsThreeSegmentConvention()
    {
        string[] segments = LocalizationOverridesPermissions.Read.Split('.');
        segments.Length.ShouldBe(3);
    }

    [Fact]
    public void Read_StartsWithGroupName()
    {
        LocalizationOverridesPermissions.Read.ShouldStartWith(
            LocalizationOverridesPermissions.GroupName + ".");
    }

    [Fact]
    public void Read_HasCorrectValue() => LocalizationOverridesPermissions.Read.ShouldBe("Localization.Overrides.Read");

    [Fact]
    public void Manage_FollowsThreeSegmentConvention()
    {
        string[] segments = LocalizationOverridesPermissions.Manage.Split('.');
        segments.Length.ShouldBe(3);
    }

    [Fact]
    public void Manage_StartsWithGroupName()
    {
        LocalizationOverridesPermissions.Manage.ShouldStartWith(
            LocalizationOverridesPermissions.GroupName + ".");
    }

    [Fact]
    public void Manage_HasCorrectValue() => LocalizationOverridesPermissions.Manage.ShouldBe("Localization.Overrides.Manage");
}
