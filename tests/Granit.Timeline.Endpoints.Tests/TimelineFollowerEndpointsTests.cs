using System.Net;
using System.Net.Http.Json;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Endpoints.Extensions;
using Granit.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Endpoints.Tests;

/// <summary>
/// Integration tests for the follower endpoints (follow, unfollow, list followers).
/// </summary>
public sealed class TimelineFollowerEndpointsTests : IAsyncDisposable
{
    private const string UserRole = "granit-timeline-user";
    private const string Prefix = "/timeline";
    private const string TestUserId = "test-user-id";

    private readonly ITimelineFollowerService _followerService = Substitute.For<ITimelineFollowerService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly WebApplication _app;
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    public TimelineFollowerEndpointsTests()
    {
        _currentUser.UserId.Returns(TestUserId);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(_followerService);
        builder.Services.AddSingleton(_currentUser);

        // Required by other endpoints but not exercised here
        builder.Services.AddSingleton(Substitute.For<ITimelineReader>());
        builder.Services.AddSingleton(Substitute.For<ITimelineWriter>());
        builder.Services.AddSingleton(Substitute.For<ITimelineNotifier>());

        _app = builder.Build();
        _app.MapTimelineEndpoints();
        _app.StartAsync().GetAwaiter().GetResult();

        _authClient = BuildClient(UserRole);
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // -- POST /{entityType}/{entityId}/follow ---------------------------------

    [Fact]
    public async Task Follow_returns_204_and_delegates_to_service()
    {
        // Act
        HttpResponseMessage response = await _authClient.PostAsync(
            $"{Prefix}/Patient/42/follow", null, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _followerService.Received(1).FollowAsync(
            TestUserId, "Patient", "42", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Follow_without_auth_returns_401()
    {
        // Act
        HttpResponseMessage response = await _anonClient.PostAsync(
            $"{Prefix}/Patient/42/follow", null, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // -- DELETE /{entityType}/{entityId}/follow --------------------------------

    [Fact]
    public async Task Unfollow_returns_204_and_delegates_to_service()
    {
        // Act
        HttpResponseMessage response = await _authClient.DeleteAsync(
            $"{Prefix}/Patient/42/follow", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _followerService.Received(1).UnfollowAsync(
            TestUserId, "Patient", "42", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unfollow_without_auth_returns_401()
    {
        // Act
        HttpResponseMessage response = await _anonClient.DeleteAsync(
            $"{Prefix}/Patient/42/follow", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // -- GET /{entityType}/{entityId}/followers --------------------------------

    [Fact]
    public async Task GetFollowers_returns_follower_ids()
    {
        // Arrange
        _followerService.GetFollowerIdsAsync("Patient", "42", Arg.Any<CancellationToken>())
            .Returns(["user-1", "user-2"]);

        // Act
        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/Patient/42/followers", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        List<string>? followers = await response.Content
            .ReadFromJsonAsync<List<string>>(TestContext.Current.CancellationToken);
        followers.ShouldNotBeNull();
        followers!.Count.ShouldBe(2);
        followers.ShouldContain("user-1");
        followers.ShouldContain("user-2");
    }

    [Fact]
    public async Task GetFollowers_empty_returns_empty_list()
    {
        // Arrange
        _followerService.GetFollowerIdsAsync("Invoice", "99", Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/Invoice/99/followers", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        List<string>? followers = await response.Content
            .ReadFromJsonAsync<List<string>>(TestContext.Current.CancellationToken);
        followers.ShouldNotBeNull();
        followers!.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetFollowers_without_auth_returns_401()
    {
        // Act
        HttpResponseMessage response = await _anonClient.GetAsync(
            $"{Prefix}/Patient/42/followers", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Follower_endpoints_with_wrong_role_returns_403()
    {
        // Arrange
        HttpClient wrongRoleClient = BuildClient("some-other-role");

        // Act
        HttpResponseMessage followResp = await wrongRoleClient.PostAsync(
            $"{Prefix}/Patient/42/follow", null, TestContext.Current.CancellationToken);
        HttpResponseMessage unfollowResp = await wrongRoleClient.DeleteAsync(
            $"{Prefix}/Patient/42/follow", TestContext.Current.CancellationToken);
        HttpResponseMessage getResp = await wrongRoleClient.GetAsync(
            $"{Prefix}/Patient/42/followers", TestContext.Current.CancellationToken);

        // Assert
        followResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        unfollowResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        getResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // -- Helpers ---------------------------------------------------------------

    private HttpClient BuildClient(string role)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }
}
