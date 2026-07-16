using Granit.Identity.Federated.EntraId.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntraId.Tests;

public sealed class EntraIdAdminOptionsAdditionalTests
{
    private readonly EntraIdAdminOptions _options = new()
    {
        TenantId = "tenant-id-123",
        ClientId = "client-id",
        ClientSecret = "client-secret",
        ServicePrincipalObjectId = "sp-object-id",
        GraphBaseUrl = "https://graph.microsoft.com",
    };

    [Fact]
    public void SectionName_IsEntraIdAdmin() => EntraIdAdminOptions.SectionName.ShouldBe("Identity:Federated:EntraId");

    [Fact]
    public void DefaultValues_AreEmptyStrings()
    {
        EntraIdAdminOptions options = new();

        options.TenantId.ShouldBe(string.Empty);
        options.ClientId.ShouldBe(string.Empty);
        options.ClientSecret.ShouldBe(string.Empty);
        options.ServicePrincipalObjectId.ShouldBe(string.Empty);
    }

    [Fact]
    public void Defaults_DefaultDomainIsNull()
    {
        EntraIdAdminOptions options = new();

        options.DefaultDomain.ShouldBeNull();
    }

    [Fact]
    public void Defaults_TimeoutSecondsIs30()
    {
        EntraIdAdminOptions options = new();

        options.TimeoutSeconds.ShouldBe(30);
    }

    [Fact]
    public void Defaults_GraphBaseUrlIsCorrect()
    {
        EntraIdAdminOptions options = new();

        options.GraphBaseUrl.ShouldBe("https://graph.microsoft.com");
    }

    [Fact]
    public void Defaults_RopcClientIdIsNull()
    {
        EntraIdAdminOptions options = new();

        options.RopcClientId.ShouldBeNull();
    }

    [Fact]
    public void GetTokenEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = _options.GetTokenEndpoint();

        endpoint.ShouldBe("https://login.microsoftonline.com/tenant-id-123/oauth2/v2.0/token");
    }

    [Fact]
    public void GetUsersEndpoint_NoParams_ReturnsBaseUrl()
    {
        string endpoint = EntraIdAdminOptions.GetUsersEndpoint();

        endpoint.ShouldContain("/v1.0/users?");
        endpoint.ShouldContain("$select=id,userPrincipalName,mail,givenName,surname,accountEnabled");
    }

    [Fact]
    public void GetUsersEndpoint_WithSearch_IncludesFilter()
    {
        string endpoint = EntraIdAdminOptions.GetUsersEndpoint(search: "alice");

        endpoint.ShouldContain("$filter=");
        endpoint.ShouldContain("alice");
    }

    [Fact]
    public void GetUsersEndpoint_WithTop_EmitsTopButNeverSkip()
    {
        // Graph rejects $skip on /users — the page size is $top and continuation is via @odata.nextLink.
        string endpoint = EntraIdAdminOptions.GetUsersEndpoint(top: 25);

        endpoint.ShouldContain("$top=25");
        endpoint.ShouldNotContain("$skip");
    }

    [Fact]
    public void GetUserEndpoint_ReturnsUrlWithUserId()
    {
        string endpoint = EntraIdAdminOptions.GetUserEndpoint("user-123");

        endpoint.ShouldContain("/v1.0/users/user-123");
        endpoint.ShouldContain("$select=");
    }

    [Fact]
    public void GetRevokeSessionsEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = EntraIdAdminOptions.GetRevokeSessionsEndpoint("user-123");

        endpoint.ShouldBe("/v1.0/users/user-123/revokeSignInSessions");
    }

    [Fact]
    public void GroupsEndpoint_IsCorrect() => EntraIdAdminOptions.GroupsEndpoint.ShouldBe("/v1.0/groups?$select=id,displayName,description");

    [Fact]
    public void GetUserGroupsEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = EntraIdAdminOptions.GetUserGroupsEndpoint("user-123");

        endpoint.ShouldContain("/v1.0/users/user-123/memberOf/microsoft.graph.group");
    }

    [Fact]
    public void GetGroupMembersRefEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = EntraIdAdminOptions.GetGroupMembersRefEndpoint("group-1");

        endpoint.ShouldBe("/v1.0/groups/group-1/members/$ref");
    }

    [Fact]
    public void GetGroupMemberEndpoint_ReturnsCorrectUrl()
    {
        string endpoint = EntraIdAdminOptions.GetGroupMemberEndpoint("group-1", "user-1");

        endpoint.ShouldBe("/v1.0/groups/group-1/members/user-1/$ref");
    }
}
