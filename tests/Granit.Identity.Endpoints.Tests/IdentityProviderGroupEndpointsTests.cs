using System.Net;
using System.Net.Http.Json;
using Granit.Identity.Endpoints.Extensions;
using Granit.Identity.Endpoints.Permissions;
using Granit.Identity.Models;
using Granit.Testing.Endpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests;

/// <summary>
/// Integration tests for identity provider group endpoints (list, user groups, add, remove).
/// </summary>
public sealed class IdentityProviderGroupEndpointsTests : IAsyncDisposable
{
    private const string Prefix = "/identity/provider";

    private static readonly string[] AdminPermissions =
    [
        IdentityPermissions.Users.Read,
        IdentityPermissions.Users.Manage,
        IdentityPermissions.Roles.Read,
        IdentityPermissions.Roles.Manage,
        IdentityPermissions.Groups.Read,
        IdentityPermissions.Groups.Manage,
        IdentityPermissions.Sessions.Read,
        IdentityPermissions.Sessions.Manage,
        IdentityPermissions.Passwords.Manage,
    ];

    private readonly IIdentityGroupManager _groupManager = Substitute.For<IIdentityGroupManager>();
    private readonly IIdentityProviderCapabilities _capabilities = Substitute.For<IIdentityProviderCapabilities>();
    private readonly WebApplication _app;
    private readonly HttpClient _adminClient;

    public IdentityProviderGroupEndpointsTests()
    {
        _capabilities.ProviderName.Returns("Test");
        _capabilities.SupportsUserCreation.Returns(true);
        _capabilities.SupportsIndividualSessionTermination.Returns(true);
        _capabilities.SupportsNativePasswordResetEmail.Returns(true);
        _capabilities.SupportsGroupHierarchy.Returns(true);
        _capabilities.SupportsCustomAttributes.Returns(true);
        _capabilities.SupportsCredentialVerification.Returns(true);
        _capabilities.MaxCustomAttributes.Returns(50);

        _groupManager.GetGroupsAsync(Arg.Any<CancellationToken>())
            .Returns([new IdentityGroup("group-1", "developers", null, [])]);

        _groupManager.GetUserGroupsAsync("user-1", Arg.Any<CancellationToken>())
            .Returns([new IdentityGroup("group-1", "developers", null, [])]);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(IdentityPermissions.Users.Read,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, IdentityPermissions.Users.Read))
            .AddPolicy(IdentityPermissions.Users.Manage,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, IdentityPermissions.Users.Manage))
            .AddPolicy(IdentityPermissions.Roles.Read,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, IdentityPermissions.Roles.Read))
            .AddPolicy(IdentityPermissions.Roles.Manage,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, IdentityPermissions.Roles.Manage))
            .AddPolicy(IdentityPermissions.Groups.Read,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, IdentityPermissions.Groups.Read))
            .AddPolicy(IdentityPermissions.Groups.Manage,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, IdentityPermissions.Groups.Manage))
            .AddPolicy(IdentityPermissions.Sessions.Read,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, IdentityPermissions.Sessions.Read))
            .AddPolicy(IdentityPermissions.Sessions.Manage,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, IdentityPermissions.Sessions.Manage))
            .AddPolicy(IdentityPermissions.Passwords.Manage,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, IdentityPermissions.Passwords.Manage));
        builder.Services.AddGranitIdentityEndpoints();
        builder.Services.AddSingleton(_groupManager);
        builder.Services.AddSingleton(_capabilities);

        _app = builder.Build();
        _app.MapGranitIdentityProvider();
        _app.StartAsync().GetAwaiter().GetResult();

        _adminClient = BuildClient(AdminPermissions);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // -- GET /groups --

    [Fact]
    public async Task GetGroups_returns_200_with_list()
    {
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/groups", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        List<IdentityGroup>? groups = await response.Content
            .ReadFromJsonAsync<List<IdentityGroup>>(TestContext.Current.CancellationToken);
        groups.ShouldNotBeNull();
        groups.Count.ShouldBe(1);
        groups[0].Name.ShouldBe("developers");
    }

    // -- GET /users/{userId}/groups --

    [Fact]
    public async Task GetUserGroups_returns_200_with_list()
    {
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/users/user-1/groups", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        List<IdentityGroup>? groups = await response.Content
            .ReadFromJsonAsync<List<IdentityGroup>>(TestContext.Current.CancellationToken);
        groups.ShouldNotBeNull();
        groups.Count.ShouldBe(1);
        groups[0].Name.ShouldBe("developers");
    }

    // -- PUT /users/{userId}/groups/{groupId} --

    [Fact]
    public async Task AddUserToGroup_returns_204()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put,
            $"{Prefix}/users/user-1/groups/group-1");

        HttpResponseMessage response = await _adminClient.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _groupManager.Received(1).AddUserToGroupAsync("user-1", "group-1", Arg.Any<CancellationToken>());
    }

    // -- DELETE /users/{userId}/groups/{groupId} --

    [Fact]
    public async Task RemoveUserFromGroup_returns_204()
    {
        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/users/user-1/groups/group-1", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _groupManager.Received(1).RemoveUserFromGroupAsync("user-1", "group-1", Arg.Any<CancellationToken>());
    }

    private HttpClient BuildClient(params string[] permissions)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, string.Join(',', permissions));
        return client;
    }
}
