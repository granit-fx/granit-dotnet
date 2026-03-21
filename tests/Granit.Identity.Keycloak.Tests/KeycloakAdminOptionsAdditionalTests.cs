using Granit.Identity.Keycloak.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.Keycloak.Tests;

public sealed class KeycloakAdminOptionsAdditionalTests
{
    private readonly KeycloakAdminOptions _options = new()
    {
        BaseUrl = "https://keycloak.example.com",
        Realm = "test-realm",
        ClientId = "admin-service",
        ClientSecret = "secret",
    };

    // ──── Feature 1: User role management ────

    [Fact]
    public void GetUserRealmRoleMappingsEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = _options.GetUserRealmRoleMappingsEndpoint("user-123");

        endpoint.ShouldBe(
            "https://keycloak.example.com/admin/realms/test-realm/users/user-123/role-mappings/realm");
    }

    [Fact]
    public void GetRoleByNameEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = _options.GetRoleByNameEndpoint("admin");

        endpoint.ShouldBe(
            "https://keycloak.example.com/admin/realms/test-realm/roles/admin");
    }

    [Fact]
    public void GetRoleByNameEndpoint_EncodesRoleName()
    {
        string endpoint = _options.GetRoleByNameEndpoint("content editor");

        endpoint.ShouldContain("roles/content%20editor");
    }

    // ──── Feature 2: Session termination ────

    [Fact]
    public void GetSessionEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = _options.GetSessionEndpoint("session-abc");

        endpoint.ShouldBe(
            "https://keycloak.example.com/admin/realms/test-realm/sessions/session-abc");
    }

    [Fact]
    public void GetUserLogoutEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = _options.GetUserLogoutEndpoint("user-123");

        endpoint.ShouldBe(
            "https://keycloak.example.com/admin/realms/test-realm/users/user-123/logout");
    }

    // ──── Feature 3: Password reset ────

    [Fact]
    public void GetExecuteActionsEmailEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = _options.GetExecuteActionsEmailEndpoint("user-123");

        endpoint.ShouldBe(
            "https://keycloak.example.com/admin/realms/test-realm/users/user-123/execute-actions-email");
    }

    [Fact]
    public void GetResetPasswordEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = _options.GetResetPasswordEndpoint("user-123");

        endpoint.ShouldBe(
            "https://keycloak.example.com/admin/realms/test-realm/users/user-123/reset-password");
    }

    // ──── Feature 5: Group management ────

    [Fact]
    public void GetGroupsEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = _options.GetGroupsEndpoint();

        endpoint.ShouldBe(
            "https://keycloak.example.com/admin/realms/test-realm/groups");
    }

    [Fact]
    public void GetUserGroupsEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = _options.GetUserGroupsEndpoint("user-123");

        endpoint.ShouldBe(
            "https://keycloak.example.com/admin/realms/test-realm/users/user-123/groups");
    }

    [Fact]
    public void GetUserGroupMembershipEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = _options.GetUserGroupMembershipEndpoint("user-123", "group-456");

        endpoint.ShouldBe(
            "https://keycloak.example.com/admin/realms/test-realm/users/user-123/groups/group-456");
    }

    // ──── Default values ────

    [Fact]
    public void TimeoutSeconds_DefaultsTo30()
    {
        KeycloakAdminOptions options = new();

        options.TimeoutSeconds.ShouldBe(30);
    }

    [Fact]
    public void DirectAccessClientId_DefaultsToNull()
    {
        KeycloakAdminOptions options = new();

        options.DirectAccessClientId.ShouldBeNull();
    }

    [Fact]
    public void DirectAccessClientId_IsSettable()
    {
        KeycloakAdminOptions options = new()
        {
            DirectAccessClientId = "my-frontend-client",
        };

        options.DirectAccessClientId.ShouldBe("my-frontend-client");
    }
}
