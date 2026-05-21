using Granit.Identity.Federated.Keycloak.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Keycloak.Tests;

public sealed class KeycloakAdminOptionsTests
{
    private readonly KeycloakAdminOptions _options = new()
    {
        BaseUrl = "https://keycloak.example.com",
        Realm = "test-realm",
        ClientId = "admin-service",
        ClientSecret = "secret",
    };

    [Fact]
    public void SectionName_IsIdentityFederatedKeycloak() =>
        KeycloakAdminOptions.SectionName.ShouldBe("Identity:Federated:Keycloak");

    [Fact]
    public void GetTokenEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = _options.GetTokenEndpoint();

        endpoint.ShouldBe(
            "https://keycloak.example.com/realms/test-realm/protocol/openid-connect/token");
    }

    [Fact]
    public void GetTokenEndpoint_TrimsTrailingSlash()
    {
        var options = new KeycloakAdminOptions
        {
            BaseUrl = "https://keycloak.example.com/",
            Realm = "test-realm",
        };

        string endpoint = options.GetTokenEndpoint();

        endpoint.ShouldBe(
            "https://keycloak.example.com/realms/test-realm/protocol/openid-connect/token");
    }

    [Fact]
    public void GetUsersEndpoint_NoParams_ReturnsBaseUsersUrl()
    {
        string endpoint = _options.GetUsersEndpoint();

        endpoint.ShouldBe("https://keycloak.example.com/admin/realms/test-realm/users");
    }

    [Fact]
    public void GetUsersEndpoint_WithSearch_IncludesSearchParam()
    {
        string endpoint = _options.GetUsersEndpoint(search: "alice");

        endpoint.ShouldBe(
            "https://keycloak.example.com/admin/realms/test-realm/users?search=alice");
    }

    [Fact]
    public void GetUsersEndpoint_WithSearchContainingSpecialChars_EncodesSearch()
    {
        string endpoint = _options.GetUsersEndpoint(search: "alice doe@test.com");

        endpoint.ShouldContain("search=alice%20doe%40test.com");
    }

    [Fact]
    public void GetUsersEndpoint_WithFirst_IncludesFirstParam()
    {
        string endpoint = _options.GetUsersEndpoint(first: 10);

        endpoint.ShouldBe(
            "https://keycloak.example.com/admin/realms/test-realm/users?first=10");
    }

    [Fact]
    public void GetUsersEndpoint_WithMax_IncludesMaxParam()
    {
        string endpoint = _options.GetUsersEndpoint(max: 25);

        endpoint.ShouldBe(
            "https://keycloak.example.com/admin/realms/test-realm/users?max=25");
    }

    [Fact]
    public void GetUsersEndpoint_AllParams_IncludesAllQueryParams()
    {
        string endpoint = _options.GetUsersEndpoint(search: "alice", first: 0, max: 10);

        endpoint.ShouldBe(
            "https://keycloak.example.com/admin/realms/test-realm/users?search=alice&first=0&max=10");
    }

    [Fact]
    public void GetUsersEndpoint_EmptySearch_ExcludesSearchParam()
    {
        string endpoint = _options.GetUsersEndpoint(search: "", first: 0, max: 10);

        endpoint.ShouldNotContain("search=");
        endpoint.ShouldContain("first=0");
        endpoint.ShouldContain("max=10");
    }

    [Fact]
    public void GetUsersEndpoint_NullSearch_ExcludesSearchParam()
    {
        string endpoint = _options.GetUsersEndpoint(search: null, first: 5);

        endpoint.ShouldNotContain("search=");
        endpoint.ShouldContain("first=5");
    }

    [Fact]
    public void GetUserEndpoint_ReturnsUrlWithUserId()
    {
        string endpoint = _options.GetUserEndpoint("user-123");

        endpoint.ShouldBe(
            "https://keycloak.example.com/admin/realms/test-realm/users/user-123");
    }

    [Fact]
    public void GetUserEndpoint_EncodesUserId()
    {
        string endpoint = _options.GetUserEndpoint("user/special chars");

        endpoint.ShouldContain("users/user%2Fspecial%20chars");
    }

    [Fact]
    public void GetRolesEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = _options.GetRolesEndpoint();

        endpoint.ShouldBe(
            "https://keycloak.example.com/admin/realms/test-realm/roles");
    }

    [Fact]
    public void GetRoleUsersEndpoint_ReturnsUrlWithRoleName()
    {
        string endpoint = _options.GetRoleUsersEndpoint("editor");

        endpoint.ShouldBe(
            "https://keycloak.example.com/admin/realms/test-realm/roles/editor/users");
    }

    [Fact]
    public void GetRoleUsersEndpoint_EncodesRoleName()
    {
        string endpoint = _options.GetRoleUsersEndpoint("content editor");

        endpoint.ShouldContain("roles/content%20editor/users");
    }

    [Fact]
    public void GetRolesEndpoint_TrimsTrailingSlash()
    {
        var options = new KeycloakAdminOptions
        {
            BaseUrl = "https://keycloak.example.com/",
            Realm = "my-realm",
        };

        string endpoint = options.GetRolesEndpoint();

        endpoint.ShouldBe("https://keycloak.example.com/admin/realms/my-realm/roles");
    }

    [Fact]
    public void GetUserSessionsEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = _options.GetUserSessionsEndpoint("user-123");

        endpoint.ShouldBe(
            "https://keycloak.example.com/admin/realms/test-realm/users/user-123/sessions");
    }

    [Fact]
    public void GetUserCredentialsEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = _options.GetUserCredentialsEndpoint("user-123");

        endpoint.ShouldBe(
            "https://keycloak.example.com/admin/realms/test-realm/users/user-123/credentials");
    }

    [Fact]
    public void GetAccountSessionsDevicesEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = _options.GetAccountSessionsDevicesEndpoint();

        endpoint.ShouldBe(
            "https://keycloak.example.com/realms/test-realm/account/sessions/devices");
    }

    [Fact]
    public void UseTokenExchangeForDeviceActivity_DefaultsToFalse() =>
        new KeycloakAdminOptions().UseTokenExchangeForDeviceActivity.ShouldBeFalse();

    [Fact]
    public void DefaultValues_AreEmptyStrings()
    {
        var options = new KeycloakAdminOptions();

        options.BaseUrl.ShouldBe(string.Empty);
        options.Realm.ShouldBe(string.Empty);
        options.ClientId.ShouldBe(string.Empty);
        options.ClientSecret.ShouldBe(string.Empty);
    }
}
