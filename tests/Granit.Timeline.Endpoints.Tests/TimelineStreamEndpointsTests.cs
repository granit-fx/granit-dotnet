using System.Net;
using System.Net.Http.Json;
using Granit.Querying;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Endpoints.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Endpoints.Tests;

/// <summary>
/// Integration tests for the GET stream endpoint.
/// </summary>
public sealed class TimelineStreamEndpointsTests : IAsyncDisposable
{
    private const string UserRole = "granit-timeline-user";
    private const string Prefix = "/timeline";

    private readonly ITimelineReader _reader = Substitute.For<ITimelineReader>();
    private readonly WebApplication _app;
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    public TimelineStreamEndpointsTests()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(_reader);

        // Required by follower/entry endpoints but not exercised here
        builder.Services.AddSingleton(Substitute.For<ITimelineWriter>());
        builder.Services.AddSingleton(Substitute.For<ITimelineFollowerService>());
        builder.Services.AddSingleton(Substitute.For<ITimelineNotifier>());
        builder.Services.AddSingleton(Substitute.For<Granit.Users.ICurrentUserService>());

        _app = builder.Build();
        _app.MapTimelineEndpoints();
        _app.StartAsync().GetAwaiter().GetResult();

        _authClient = BuildClient(UserRole);
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // -- GET /{entityType}/{entityId} -----------------------------------------

    [Fact]
    public async Task GetStream_returns_paginated_entries()
    {
        // Arrange
        var entry = new TimelineStreamEntry
        {
            Id = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            EntryType = TimelineStreamEntryType.Comment,
            AuthorId = "user-1",
            AuthorName = "Alice",
            Body = "Hello",
        };

        _reader.GetStreamAsync("Patient", "42", 1, QueryingDefaults.DefaultPageSize, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<TimelineStreamEntry>([entry], 1, HasMore: false));

        // Act
        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/Patient/42", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        PagedResult<TimelineStreamEntry>? page = await response.Content
            .ReadFromJsonAsync<PagedResult<TimelineStreamEntry>>(TestContext.Current.CancellationToken);
        page.ShouldNotBeNull();
        page!.TotalCount.ShouldBe(1);
        page.Items.Count.ShouldBe(1);
        page.Items[0].Body.ShouldBe("Hello");
        page.Items[0].AuthorName.ShouldBe("Alice");
    }

    [Fact]
    public async Task GetStream_empty_returns_empty_page()
    {
        // Arrange
        _reader.GetStreamAsync("Invoice", "99", 1, QueryingDefaults.DefaultPageSize, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<TimelineStreamEntry>([], 0, HasMore: false));

        // Act
        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/Invoice/99", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        PagedResult<TimelineStreamEntry>? page = await response.Content
            .ReadFromJsonAsync<PagedResult<TimelineStreamEntry>>(TestContext.Current.CancellationToken);
        page.ShouldNotBeNull();
        page!.TotalCount.ShouldBe(0);
        page.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetStream_passes_page_and_pageSize_parameters()
    {
        // Arrange
        _reader.GetStreamAsync("Patient", "1", 3, 10, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<TimelineStreamEntry>([], 50, HasMore: false));

        // Act
        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/Patient/1?page=3&pageSize=10", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _reader.Received(1).GetStreamAsync("Patient", "1", 3, 10, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetStream_without_auth_returns_401()
    {
        // Act
        HttpResponseMessage response = await _anonClient.GetAsync(
            $"{Prefix}/Patient/42", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetStream_with_wrong_role_returns_403()
    {
        // Arrange
        HttpClient wrongRoleClient = BuildClient("some-other-role");

        // Act
        HttpResponseMessage response = await wrongRoleClient.GetAsync(
            $"{Prefix}/Patient/42", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // -- Helpers ---------------------------------------------------------------

    private HttpClient BuildClient(string role)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }
}
