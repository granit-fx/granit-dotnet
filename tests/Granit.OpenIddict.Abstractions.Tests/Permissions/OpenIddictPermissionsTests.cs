using Granit.OpenIddict.Permissions;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Abstractions.Tests.Permissions;

public sealed class OpenIddictPermissionsTests
{
    [Fact]
    public void GroupName_IsOpenIddict() =>
        OpenIddictPermissions.GroupName.ShouldBe("OpenIddict");

    [Theory]
    [InlineData("OpenIddict.Users.Impersonate")]
    [InlineData("OpenIddict.Applications.Read")]
    [InlineData("OpenIddict.Applications.Manage")]
    [InlineData("OpenIddict.Applications.Rotate")]
    [InlineData("OpenIddict.Scopes.Read")]
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
