using Granit.Identity.Federated.EntraId.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntraId.Tests;

public sealed class EntraIdAdminOptionsTests
{
    [Fact]
    public void GetTokenEndpoint_ReturnsCorrectUrlWithTenantId()
    {
        var options = new EntraIdAdminOptions { TenantId = "my-tenant-id" };

        string endpoint = options.GetTokenEndpoint();

        endpoint.ShouldBe("https://login.microsoftonline.com/my-tenant-id/oauth2/v2.0/token");
    }

    [Fact]
    public void GetUsersEndpoint_WithoutParams_ReturnsBaseEndpointWithSelect()
    {
        string endpoint = EntraIdAdminOptions.GetUsersEndpoint();

        endpoint.ShouldStartWith("/v1.0/users?");
        endpoint.ShouldContain("$select=");
    }

    [Fact]
    public void GetUsersEndpoint_WithSearch_IncludesFilterParam()
    {
        string endpoint = EntraIdAdminOptions.GetUsersEndpoint(search: "alice");

        endpoint.ShouldContain("$filter=");
        endpoint.ShouldContain("alice");
    }

    [Fact]
    public void GetUsersEndpoint_WithPagination_IncludesSkipAndTopParams()
    {
        string endpoint = EntraIdAdminOptions.GetUsersEndpoint(skip: 10, top: 25);

        endpoint.ShouldContain("$skip=10");
        endpoint.ShouldContain("$top=25");
    }

    [Fact]
    public void GetUsersEndpoint_WithSearchContainingSpecialChars_EscapesSearch()
    {
        string endpoint = EntraIdAdminOptions.GetUsersEndpoint(search: "test user");

        endpoint.ShouldContain("test%20user");
    }

    [Fact]
    public void GetUserEndpoint_ReturnsCorrectUrlWithEscapedUserId()
    {
        string endpoint = EntraIdAdminOptions.GetUserEndpoint("user-123");

        endpoint.ShouldStartWith("/v1.0/users/user-123");
        endpoint.ShouldContain("$select=");
    }

    [Fact]
    public void GetUserEndpoint_EscapesSpecialCharacters()
    {
        string endpoint = EntraIdAdminOptions.GetUserEndpoint("user@domain.com");

        endpoint.ShouldContain("user%40domain.com");
    }

    [Fact]
    public void GetServicePrincipalAppRolesEndpoint_ReturnsCorrectUrl()
    {
        var options = new EntraIdAdminOptions { ServicePrincipalObjectId = "sp-obj-id" };

        string endpoint = options.GetServicePrincipalAppRolesEndpoint();

        endpoint.ShouldBe("/v1.0/servicePrincipals/sp-obj-id/appRoles");
    }

    [Fact]
    public void GetGroupsEndpoint_ReturnsStaticUrl()
    {
        const string endpoint = EntraIdAdminOptions.GroupsEndpoint;

        endpoint.ShouldBe("/v1.0/groups?$select=id,displayName,description");
    }

    [Fact]
    public void GetRevokeSessionsEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = EntraIdAdminOptions.GetRevokeSessionsEndpoint("user-abc");

        endpoint.ShouldBe("/v1.0/users/user-abc/revokeSignInSessions");
    }

    [Fact]
    public void GetUserGroupsEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = EntraIdAdminOptions.GetUserGroupsEndpoint("user-abc");

        endpoint.ShouldContain("/v1.0/users/user-abc/memberOf/microsoft.graph.group");
        endpoint.ShouldContain("$select=");
    }

    [Fact]
    public void GetGroupMembersRefEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = EntraIdAdminOptions.GetGroupMembersRefEndpoint("grp-1");

        endpoint.ShouldBe("/v1.0/groups/grp-1/members/$ref");
    }

    [Fact]
    public void GetGroupMemberEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = EntraIdAdminOptions.GetGroupMemberEndpoint("grp-1", "user-1");

        endpoint.ShouldBe("/v1.0/groups/grp-1/members/user-1/$ref");
    }

    [Fact]
    public void GetAuditSignInsEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = EntraIdAdminOptions.GetAuditSignInsEndpoint("user-abc");

        endpoint.ShouldContain("/v1.0/auditLogs/signIns");
        endpoint.ShouldContain("user-abc");
        endpoint.ShouldContain("$top=25");
    }

    [Fact]
    public void GetAuditSignInsEndpoint_WithCustomTop_ReturnsCorrectUrl()
    {
        string endpoint = EntraIdAdminOptions.GetAuditSignInsEndpoint("user-abc", top: 50);

        endpoint.ShouldContain("$top=50");
    }

    [Fact]
    public void GetUserAppRoleAssignmentsEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = EntraIdAdminOptions.GetUserAppRoleAssignmentsEndpoint("user-abc");

        endpoint.ShouldBe("/v1.0/users/user-abc/appRoleAssignments");
    }

    [Fact]
    public void GetUserPasswordChangeDateEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = EntraIdAdminOptions.GetUserPasswordChangeDateEndpoint("user-abc");

        endpoint.ShouldContain("/v1.0/users/user-abc");
        endpoint.ShouldContain("lastPasswordChangeDateTime");
    }

    [Fact]
    public void SectionName_IsEntraIdAdmin() =>
        EntraIdAdminOptions.SectionName.ShouldBe("Identity:Federated:EntraId");
}
