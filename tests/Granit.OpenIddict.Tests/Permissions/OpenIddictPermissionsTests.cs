using Granit.OpenIddict.Permissions;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Permissions;

public sealed class OpenIddictPermissionsTests
{
    [Fact]
    public void GroupName_IsOpenIddict() =>
        OpenIddictPermissions.GroupName.ShouldBe("OpenIddict");

    [Theory]
    [InlineData("OpenIddict.Users.Read")]
    [InlineData("OpenIddict.Users.Create")]
    [InlineData("OpenIddict.Users.Manage")]
    [InlineData("OpenIddict.Users.Delete")]
    [InlineData("OpenIddict.Users.Impersonate")]
    [InlineData("OpenIddict.Roles.Read")]
    [InlineData("OpenIddict.Roles.Create")]
    [InlineData("OpenIddict.Roles.Delete")]
    [InlineData("OpenIddict.Groups.Read")]
    [InlineData("OpenIddict.Groups.Create")]
    [InlineData("OpenIddict.Groups.Manage")]
    [InlineData("OpenIddict.Groups.Delete")]
    [InlineData("OpenIddict.Applications.Read")]
    [InlineData("OpenIddict.Applications.Create")]
    [InlineData("OpenIddict.Applications.Manage")]
    [InlineData("OpenIddict.Applications.Delete")]
    [InlineData("OpenIddict.Applications.Rotate")]
    [InlineData("OpenIddict.Scopes.Read")]
    [InlineData("OpenIddict.Scopes.Create")]
    [InlineData("OpenIddict.Scopes.Manage")]
    [InlineData("OpenIddict.Scopes.Delete")]
    [InlineData("OpenIddict.Authorizations.Read")]
    [InlineData("OpenIddict.Authorizations.Revoke")]
    public void AllPermissions_FollowThreeSegmentConvention(string permission)
    {
        string[] segments = permission.Split('.');
        segments.Length.ShouldBe(3, $"Permission '{permission}' must have exactly 3 segments.");
        segments[0].ShouldBe("OpenIddict");
    }
}
