using System.Net;
using Granit.Identity.Endpoints.Extensions;
using Granit.Identity.Endpoints.Internal;
using Granit.Identity.Endpoints.Permissions;
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
    private const string AdminRole = "granit-identity-admin";
    private const string Prefix = "/identity/users";

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
    public async Task Erase_wrong_role_returns_403()
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
    public async Task Pseudonymize_wrong_role_returns_403()
    {
        HttpResponseMessage response = await _userClient.PatchAsync(
            $"{Prefix}/user-1/pseudonymize", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private HttpClient BuildClient(string role)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }
}
