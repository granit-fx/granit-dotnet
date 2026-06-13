using System.Net;
using Granit.Identity.Endpoints.Extensions;
using Granit.Identity.Endpoints.Permissions;
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
/// Integration tests for identity user cache GDPR endpoints (erase, pseudonymize).
/// </summary>
public sealed class IdentityUserCacheGdprEndpointsTests : IAsyncDisposable
{
    private const string Prefix = "/identity/users";

    private static readonly string[] AdminPermissions =
    [
        IdentityPermissions.Users.Read,
        IdentityPermissions.Users.Sync,
        IdentityPermissions.Users.Delete,
    ];

    private readonly IUserLookupService _lookupService = Substitute.For<IUserLookupService>();
    private readonly IUserCacheStats _cacheStats = Substitute.For<IUserCacheStats>();
    private readonly WebApplication _app;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _userClient;

    public IdentityUserCacheGdprEndpointsTests()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(IdentityPermissions.Users.Read,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, IdentityPermissions.Users.Read))
            .AddPolicy(IdentityPermissions.Users.Sync,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, IdentityPermissions.Users.Sync))
            .AddPolicy(IdentityPermissions.Users.Delete,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, IdentityPermissions.Users.Delete));
        builder.Services.AddGranitIdentityEndpoints();
        builder.Services.AddSingleton(_lookupService);
        builder.Services.AddSingleton(_cacheStats);

        _app = builder.Build();
        _app.MapGranitIdentityUserCache();
        _app.StartAsync().GetAwaiter().GetResult();

        _adminClient = BuildClient(AdminPermissions);
        // Authenticated but holds only a read permission — must be forbidden (not 401) on the delete endpoints.
        _userClient = BuildClient(IdentityPermissions.Users.Read);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // -- DELETE /{userId} --

    [Fact]
    public async Task Erase_returns_204()
    {
        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/user-1", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _lookupService.Received(1).DeleteByIdAsync("user-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Erase_without_permission_returns_403()
    {
        HttpResponseMessage response = await _userClient.DeleteAsync(
            $"{Prefix}/user-1", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // -- PATCH /{userId}/pseudonymize --

    [Fact]
    public async Task Pseudonymize_returns_204()
    {
        HttpResponseMessage response = await _adminClient.PatchAsync(
            $"{Prefix}/user-1/pseudonymize", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _lookupService.Received(1).PseudonymizeByIdAsync("user-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Pseudonymize_without_permission_returns_403()
    {
        HttpResponseMessage response = await _userClient.PatchAsync(
            $"{Prefix}/user-1/pseudonymize", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private HttpClient BuildClient(params string[] permissions)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, string.Join(',', permissions));
        return client;
    }
}
