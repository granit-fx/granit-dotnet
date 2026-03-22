using System.Net;
using System.Net.Http.Json;
using Granit.Identity.Endpoints.Extensions;
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
/// Integration tests for identity provider user endpoints (list, get, create, update, enable/disable).
/// </summary>
public sealed class IdentityProviderUserEndpointsTests : IAsyncDisposable
{
    private const string AdminRole = "granit-identity-admin";
    private const string Prefix = "/identity/provider";

    private readonly IIdentityUserReader _userReader = Substitute.For<IIdentityUserReader>();
    private readonly IIdentityUserWriter _userWriter = Substitute.For<IIdentityUserWriter>();
    private readonly IIdentityProviderCapabilities _capabilities = Substitute.For<IIdentityProviderCapabilities>();
    private readonly WebApplication _app;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _anonClient;
    private readonly HttpClient _userClient;

    public IdentityProviderUserEndpointsTests()
    {
        _capabilities.ProviderName.Returns("Test");
        _capabilities.SupportsUserCreation.Returns(true);
        _capabilities.SupportsIndividualSessionTermination.Returns(true);
        _capabilities.SupportsNativePasswordResetEmail.Returns(true);
        _capabilities.SupportsGroupHierarchy.Returns(true);
        _capabilities.SupportsCustomAttributes.Returns(true);
        _capabilities.SupportsCredentialVerification.Returns(true);
        _capabilities.MaxCustomAttributes.Returns(50);

        _userWriter.CreateUserAsync(Arg.Any<IdentityUserCreate>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIdentityUser>(new FakeIdentityUser("new-id", "newuser", "new@test.com", "New", "User", true)));

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorization();
        builder.Services.AddGranitIdentityEndpoints();
        builder.Services.AddSingleton(_userReader);
        builder.Services.AddSingleton(_userWriter);
        builder.Services.AddSingleton(_capabilities);

        _app = builder.Build();
        _app.MapIdentityProviderEndpoints();
        _app.StartAsync().GetAwaiter().GetResult();

        _adminClient = BuildClient(AdminRole);
        _anonClient = _app.GetTestClient();
        _userClient = BuildClient("regular-user");
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // -- GET /users --

    [Fact]
    public async Task GetUsers_returns_200_with_list()
    {
        _userReader.GetUsersAsync(null, null, null, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<IIdentityUser>)[new FakeIdentityUser("user-1", "jdoe", "jdoe@test.com", "John", "Doe", true)]);

        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/users", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        List<FakeIdentityUser>? users = await response.Content
            .ReadFromJsonAsync<List<FakeIdentityUser>>(TestContext.Current.CancellationToken);
        users.ShouldNotBeNull();
        users.Count.ShouldBe(1);
        users[0].Username.ShouldBe("jdoe");
    }

    [Fact]
    public async Task GetUsers_without_auth_returns_401()
    {
        HttpResponseMessage response = await _anonClient.GetAsync(
            $"{Prefix}/users", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // -- GET /users/{userId} --

    [Fact]
    public async Task GetUser_existing_returns_200()
    {
        _userReader.GetUserAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIdentityUser?>(new FakeIdentityUser("user-1", "jdoe", "jdoe@test.com", "John", "Doe", true)));

        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/users/user-1", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        FakeIdentityUser? user = await response.Content
            .ReadFromJsonAsync<FakeIdentityUser>(TestContext.Current.CancellationToken);
        user.ShouldNotBeNull();
        user.UserId.ShouldBe("user-1");
    }

    [Fact]
    public async Task GetUser_unknown_returns_404()
    {
        _userReader.GetUserAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIdentityUser?>(null));

        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/users/nonexistent", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // -- POST /users --

    [Fact]
    public async Task CreateUser_returns_201()
    {
        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            $"{Prefix}/users",
            new { Username = "newuser", Email = "new@test.com", FirstName = "New", LastName = "User", Enabled = true },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        FakeIdentityUser? created = await response.Content
            .ReadFromJsonAsync<FakeIdentityUser>(TestContext.Current.CancellationToken);
        created.ShouldNotBeNull();
        created.UserId.ShouldBe("new-id");
    }

    [Fact]
    public async Task CreateUser_unsupported_returns_501()
    {
        _capabilities.SupportsUserCreation.Returns(false);

        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            $"{Prefix}/users",
            new { Username = "newuser", Email = "new@test.com", FirstName = "New", LastName = "User", Enabled = true },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    // -- PUT /users/{userId} --

    [Fact]
    public async Task UpdateUser_returns_204()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{Prefix}/users/user-1")
        {
            Content = JsonContent.Create(new { Email = "updated@test.com", FirstName = "Updated" }),
        };

        HttpResponseMessage response = await _adminClient.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    // -- PATCH /users/{userId}/enabled --

    [Fact]
    public async Task SetUserEnabled_returns_204()
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"{Prefix}/users/user-1/enabled")
        {
            Content = JsonContent.Create(new { Enabled = false }),
        };

        HttpResponseMessage response = await _adminClient.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    // -- Authorization --

    [Fact]
    public async Task CreateUser_wrong_role_returns_403()
    {
        HttpResponseMessage response = await _userClient.PostAsJsonAsync(
            $"{Prefix}/users",
            new { Username = "newuser", Email = "new@test.com", FirstName = "New", LastName = "User", Enabled = true },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private HttpClient BuildClient(string role)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }
}
