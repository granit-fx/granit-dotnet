using System.Net;
using System.Net.Http.Json;
using Granit.Identity.Endpoints.Extensions;
using Granit.Identity.Endpoints.Permissions;
using Granit.Identity.Models;
using Granit.Testing.Endpoints;
using Granit.Tests.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests;

/// <summary>
/// Integration tests for identity provider role endpoints (list, members, assign, remove).
/// </summary>
public sealed class IdentityProviderRoleEndpointsTests : IAsyncDisposable
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

    private readonly IIdentityRoleManager _roleManager = Substitute.For<IIdentityRoleManager>();
    private readonly IIdentityProviderCapabilities _capabilities = Substitute.For<IIdentityProviderCapabilities>();
    private readonly WebApplication _app;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _anonClient;

    public IdentityProviderRoleEndpointsTests()
    {
        _capabilities.ProviderName.Returns("Test");
        _capabilities.SupportsUserCreation.Returns(true);
        _capabilities.SupportsIndividualSessionTermination.Returns(true);
        _capabilities.SupportsNativePasswordResetEmail.Returns(true);
        _capabilities.SupportsGroupHierarchy.Returns(true);
        _capabilities.SupportsCustomAttributes.Returns(true);
        _capabilities.SupportsCredentialVerification.Returns(true);
        _capabilities.MaxCustomAttributes.Returns(50);

        _roleManager.GetRolesAsync(Arg.Any<CancellationToken>())
            .Returns([new IdentityRole("role-1", "admin", null)]);

        _roleManager.GetRoleMembersAsync("admin", Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<IIdentityUser>)[new FakeIdentityUser("user-1", "jdoe", "jdoe@test.com", "John", "Doe", true)]);

        _roleManager.GetUserRolesAsync("user-1", Arg.Any<CancellationToken>())
            .Returns([new IdentityRole("role-1", "admin", null)]);

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
        builder.Services.AddSingleton(_roleManager);
        builder.Services.AddSingleton(_capabilities);

        _app = builder.Build();
        _app.MapGranitIdentityProvider();
        _app.StartAsync().GetAwaiter().GetResult();

        _adminClient = BuildClient(AdminPermissions);
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // -- GET /roles --

    [Fact]
    public async Task GetRoles_returns_200_with_list()
    {
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/roles", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        List<IdentityRole>? roles = await response.Content
            .ReadFromJsonAsync<List<IdentityRole>>(TestContext.Current.CancellationToken);
        roles.ShouldNotBeNull();
        roles.Count.ShouldBe(1);
        roles[0].Name.ShouldBe("admin");
    }

    [Fact]
    public async Task GetRoles_without_auth_returns_401()
    {
        HttpResponseMessage response = await _anonClient.GetAsync(
            $"{Prefix}/roles", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // -- GET /roles/{roleName}/members --

    [Fact]
    public async Task GetRoleMembers_returns_200_with_list()
    {
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/roles/admin/members", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        List<FakeIdentityUser>? members = await response.Content
            .ReadFromJsonAsync<List<FakeIdentityUser>>(TestContext.Current.CancellationToken);
        members.ShouldNotBeNull();
        members.Count.ShouldBe(1);
        members[0].Username.ShouldBe("jdoe");
    }

    // -- GET /users/{userId}/roles --

    [Fact]
    public async Task GetUserRoles_returns_200_with_list()
    {
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/users/user-1/roles", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        List<IdentityRole>? roles = await response.Content
            .ReadFromJsonAsync<List<IdentityRole>>(TestContext.Current.CancellationToken);
        roles.ShouldNotBeNull();
        roles.Count.ShouldBe(1);
        roles[0].Name.ShouldBe("admin");
    }

    // -- PUT /users/{userId}/roles/{roleName} --

    [Fact]
    public async Task AssignRole_returns_204()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put,
            $"{Prefix}/users/user-1/roles/admin");

        HttpResponseMessage response = await _adminClient.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _roleManager.Received(1).AssignRoleAsync("user-1", "admin", Arg.Any<CancellationToken>());
    }

    // -- DELETE /users/{userId}/roles/{roleName} --

    [Fact]
    public async Task RemoveRole_returns_204()
    {
        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/users/user-1/roles/admin", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _roleManager.Received(1).RemoveRoleAsync("user-1", "admin", Arg.Any<CancellationToken>());
    }

    private HttpClient BuildClient(params string[] permissions)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, string.Join(',', permissions));
        return client;
    }
}
