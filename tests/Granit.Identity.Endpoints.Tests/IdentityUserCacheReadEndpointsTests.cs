using System.Net;
using System.Net.Http.Json;
using Granit.Identity.Endpoints.Extensions;
using Granit.Identity.Endpoints.Internal;
using Granit.Identity.Models;
using Granit.Querying;
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
/// Integration tests for identity user cache read endpoints (search, get, batch).
/// </summary>
public sealed class IdentityUserCacheReadEndpointsTests : IAsyncDisposable
{
    private const string AdminRole = "granit-identity-admin";
    private const string Prefix = "/identity/users";

    private readonly IUserLookupService _lookupService = Substitute.For<IUserLookupService>();
    private readonly IUserCacheStats _cacheStats = Substitute.For<IUserCacheStats>();
    private readonly WebApplication _app;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _anonClient;

    public IdentityUserCacheReadEndpointsTests()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorization();
        builder.Services.AddGranitIdentityEndpoints();
        builder.Services.AddSingleton(_lookupService);
        builder.Services.AddSingleton(_cacheStats);

        _app = builder.Build();
        _app.MapIdentityUserCacheEndpoints();
        _app.StartAsync().GetAwaiter().GetResult();

        _adminClient = BuildClient(AdminRole);
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // -- GET / (Search) --

    [Fact]
    public async Task Search_returns_200_with_results()
    {
        _lookupService.SearchAsync("john", 1, 20, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<IIdentityUser>(
                [new FakeIdentityUser("user-1", "jdoe", "jdoe@test.com", "John", "Doe", true)], 1, HasMore: false)));

        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}?search=john", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        PagedResult<FakeIdentityUser>? result = await response.Content
            .ReadFromJsonAsync<PagedResult<FakeIdentityUser>>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(1);
        result.TotalCount.ShouldBe(1);
        result.Items[0].Username.ShouldBe("jdoe");
    }

    [Fact]
    public async Task Search_without_auth_returns_401()
    {
        HttpResponseMessage response = await _anonClient.GetAsync(
            $"{Prefix}?search=john", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // -- GET /{userId} --

    [Fact]
    public async Task GetById_existing_user_returns_200()
    {
        _lookupService.FindByIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIdentityUser?>(new FakeIdentityUser("user-1", "jdoe", "jdoe@test.com", "John", "Doe", true)));

        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/user-1", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        FakeIdentityUser? user = await response.Content
            .ReadFromJsonAsync<FakeIdentityUser>(TestContext.Current.CancellationToken);
        user.ShouldNotBeNull();
        user.UserId.ShouldBe("user-1");
    }

    [Fact]
    public async Task GetById_unknown_user_returns_404()
    {
        _lookupService.FindByIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIdentityUser?>(null));

        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/nonexistent", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // -- POST /batch --

    [Fact]
    public async Task BatchResolve_returns_200_with_results()
    {
        _lookupService.FindByIdsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<IIdentityUser>)[
                new FakeIdentityUser("user-1", "jdoe", "jdoe@test.com", "John", "Doe", true),
                new FakeIdentityUser("user-2", "jane", "jane@test.com", "Jane", "Smith", true),
            ]);

        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            $"{Prefix}/batch",
            new { UserIds = new[] { "user-1", "user-2" } },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        List<FakeIdentityUser>? users = await response.Content
            .ReadFromJsonAsync<List<FakeIdentityUser>>(TestContext.Current.CancellationToken);
        users.ShouldNotBeNull();
        users.Count.ShouldBe(2);
    }

    private HttpClient BuildClient(string role)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }
}
