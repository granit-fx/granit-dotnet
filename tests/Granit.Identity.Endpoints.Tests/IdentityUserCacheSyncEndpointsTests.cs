using System.Net;
using System.Net.Http.Json;
using Granit.Identity.Endpoints.Extensions;
using Granit.Identity.Endpoints.Internal;
using Granit.Identity.Endpoints.Permissions;
using Granit.Identity.Models;
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
    private const string AdminRole = "granit-identity-admin";
    private const string Prefix = "/identity/users";

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
                policy => policy.RequireRole(AdminRole))
            .AddPolicy(IdentityPermissions.Users.Sync,
                policy => policy.RequireRole(AdminRole))
            .AddPolicy(IdentityPermissions.Users.Delete,
                policy => policy.RequireRole(AdminRole));
        builder.Services.AddGranitIdentityEndpoints();
        builder.Services.AddSingleton(_lookupService);
        builder.Services.AddSingleton(_cacheStats);

        _app = builder.Build();
        _app.MapGranitIdentityUserCache();
        _app.StartAsync().GetAwaiter().GetResult();

        _adminClient = BuildClient(AdminRole);
        _userClient = BuildClient("regular-user");
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
    public async Task Sync_wrong_role_returns_403()
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
    public async Task SyncStale_wrong_role_returns_403()
    {
        HttpResponseMessage response = await _userClient.PostAsync(
            $"{Prefix}/sync-stale", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private HttpClient BuildClient(string role)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }
}
