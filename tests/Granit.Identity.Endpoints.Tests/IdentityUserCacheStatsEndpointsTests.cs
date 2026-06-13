using System.Net;
using System.Net.Http.Json;
using Granit.Identity.Endpoints.Dtos;
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
/// Integration tests for the identity user cache stats endpoint.
/// </summary>
public sealed class IdentityUserCacheStatsEndpointsTests : IAsyncDisposable
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

    public IdentityUserCacheStatsEndpointsTests()
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
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task GetStats_returns_200_with_stats()
    {
        _cacheStats.GetCountAsync(Arg.Any<CancellationToken>()).Returns(100);
        _cacheStats.GetStaleCountAsync(Arg.Any<CancellationToken>()).Returns(5);
        _cacheStats.GetSyncRangeAsync(Arg.Any<CancellationToken>())
            .Returns((
                (DateTimeOffset?)new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                (DateTimeOffset?)new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero)));

        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/stats", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        IdentityUserCacheStatsResponse? stats = await response.Content
            .ReadFromJsonAsync<IdentityUserCacheStatsResponse>(TestContext.Current.CancellationToken);
        stats.ShouldNotBeNull();
        stats.TotalEntries.ShouldBe(100);
        stats.StaleEntries.ShouldBe(5);
        stats.OldestSyncAt.ShouldNotBeNull();
        stats.NewestSyncAt.ShouldNotBeNull();
    }

    private HttpClient BuildClient(params string[] permissions)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, string.Join(',', permissions));
        return client;
    }
}
