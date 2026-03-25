using Granit.Authentication.ApiKeys.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Endpoints.Tests.Permissions;

public sealed class ApiKeyPermissionsTests
{
    [Fact]
    public void GroupName_Is_AuthenticationApiKeys() =>
        ApiKeyPermissions.GroupName.ShouldBe("AuthenticationApiKeys");

    [Theory]
    [InlineData("AuthenticationApiKeys.Keys.Read")]
    [InlineData("AuthenticationApiKeys.Keys.Create")]
    [InlineData("AuthenticationApiKeys.Keys.Revoke")]
    [InlineData("AuthenticationApiKeys.Keys.Rotate")]
    [InlineData("AuthenticationApiKeys.Keys.UpdateScopes")]
    public void Permission_Constants_Follow_Convention(string permission) =>
        permission.ShouldStartWith(ApiKeyPermissions.GroupName);

    [Fact]
    public void Read_Permission_Value() =>
        ApiKeyPermissions.Keys.Read.ShouldBe("AuthenticationApiKeys.Keys.Read");

    [Fact]
    public void Create_Permission_Value() =>
        ApiKeyPermissions.Keys.Create.ShouldBe("AuthenticationApiKeys.Keys.Create");

    [Fact]
    public void Revoke_Permission_Value() =>
        ApiKeyPermissions.Keys.Revoke.ShouldBe("AuthenticationApiKeys.Keys.Revoke");

    [Fact]
    public void Rotate_Permission_Value() =>
        ApiKeyPermissions.Keys.Rotate.ShouldBe("AuthenticationApiKeys.Keys.Rotate");

    [Fact]
    public void UpdateScopes_Permission_Value() =>
        ApiKeyPermissions.Keys.UpdateScopes.ShouldBe("AuthenticationApiKeys.Keys.UpdateScopes");
}
