using System.Net;
using System.Net.Http.Json;
using Granit.Identity.Endpoints.Extensions;
using Granit.Identity.Endpoints.Permissions;
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
/// Integration tests for identity user cache sync endpoints.
/// </summary>
public sealed class IdentityUserCacheSyncEndpointsTests : IAsyncDisposable
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

    public IdentityUserCacheSyncEndpointsTests()
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
        // Authenticated but holds only a read permission — must be forbidden (not 401) on the sync endpoints.
        _userClient = BuildClient(IdentityPermissions.Users.Read);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // -- POST /sync --

    [Fact]
    public async Task Sync_returns_200_with_refreshed_users()
    {
        _lookupService.RefreshByIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIdentityUser?>(new FakeIdentityUser("user-1", "jdoe", "jdoe@test.com", "John", "Doe", true)));
        _lookupService.RefreshByIdAsync("user-2", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIdentityUser?>(null));

        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            $"{Prefix}/sync",
            new { UserIds = new[] { "user-1", "user-2" } },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        List<FakeIdentityUser>? users = await response.Content
            .ReadFromJsonAsync<List<FakeIdentityUser>>(TestContext.Current.CancellationToken);
        users.ShouldNotBeNull();
        users.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Sync_without_permission_returns_403()
    {
        HttpResponseMessage response = await _userClient.PostAsJsonAsync(
            $"{Prefix}/sync",
            new { UserIds = new[] { "user-1" } },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // -- POST /sync-all --

    [Fact]
    public async Task SyncAll_returns_200_with_synced_count()
    {
        _lookupService.RefreshAllAsync(Arg.Any<CancellationToken>())
            .Returns(150);

        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/sync-all", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("150");
    }

    // -- POST /sync-stale --

    [Fact]
    public async Task SyncStale_returns_200_with_refreshed_count()
    {
        _lookupService.RefreshStaleAsync(Arg.Any<CancellationToken>())
            .Returns(25);

        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/sync-stale", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("25");
    }

    [Fact]
    public async Task SyncStale_without_permission_returns_403()
    {
        HttpResponseMessage response = await _userClient.PostAsync(
            $"{Prefix}/sync-stale", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private HttpClient BuildClient(params string[] permissions)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, string.Join(',', permissions));
        return client;
    }
}
