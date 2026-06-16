using System.Net;
using System.Net.Http.Json;
using Granit.Authorization;
using Granit.QueryEngine;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Endpoints.Dtos;
using Granit.Timeline.Endpoints.Extensions;
using Granit.Timeline.Endpoints.Permissions;
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
    private const string Prefix = "/timeline";

    private readonly ITimelineReader _reader = Substitute.For<ITimelineReader>();
    private readonly IPermissionChecker _permissionChecker = Substitute.For<IPermissionChecker>();
    private readonly WebApplication _app;
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    public TimelineStreamEndpointsTests()
    {
        // Default: allow InternalNotes.Read so existing tests see all entry types
        _permissionChecker.IsGrantedAsync(TimelinePermissions.InternalNotes.Read, Arg.Any<CancellationToken>())
            .Returns(true);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(TimelinePermissions.Entries.Read, policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TimelinePermissions.Entries.Read))
            .AddPolicy(TimelinePermissions.Entries.Create, policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TimelinePermissions.Entries.Create))
            .AddPolicy(TimelinePermissions.Followers.Manage, policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TimelinePermissions.Followers.Manage));
        builder.Services.AddSingleton(_reader);
        builder.Services.AddSingleton(_permissionChecker);

        // Required by follower/entry endpoints but not exercised here
        builder.Services.AddSingleton(Substitute.For<ITimelineWriter>());
        builder.Services.AddSingleton(Substitute.For<ITimelineFollowerService>());
        builder.Services.AddSingleton(Substitute.For<ITimelineNotifier>());
        builder.Services.AddSingleton(Substitute.For<Granit.Users.ICurrentUserService>());

        // Reactions infra (story C1-C3) — stream endpoint enriches entries
        // with the per-emoji reaction summary; no reactions are exercised here
        // so the substitute returns an empty list by default.
        builder.Services.AddSingleton(Substitute.For<IReactionReader>());
        builder.Services.AddSingleton(Substitute.For<IReactionWriter>());

        _app = builder.Build();
        _app.MapGranitTimeline();
        _app.StartAsync().GetAwaiter().GetResult();

        _authClient = BuildClient(
            TimelinePermissions.Entries.Read,
            TimelinePermissions.Entries.Create,
            TimelinePermissions.Followers.Manage);
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

        _reader.GetStreamAsync("Patient", "42", 1, QueryEngineDefaults.DefaultPageSize, Arg.Any<CancellationToken>())
            .Returns(new TimelineStreamResult(new PagedResult<TimelineStreamEntry>([entry], 1, HasMore: false), []));

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
    public async Task GetStream_surfaces_external_federation_fields()
    {
        // Arrange — an external (projected) entry carries provenance the front renders.
        var entry = new TimelineStreamEntry
        {
            Id = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            EntryType = TimelineStreamEntryType.SystemLog,
            Body = "Audit event",
            Origin = TimelineEntryOrigin.External,
            SourceKey = "auditing",
            SourceId = "audit-42",
        };

        _reader.GetStreamAsync("Patient", "42", 1, QueryEngineDefaults.DefaultPageSize, Arg.Any<CancellationToken>())
            .Returns(new TimelineStreamResult(new PagedResult<TimelineStreamEntry>([entry], 1, HasMore: false), []));

        // Act
        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/Patient/42", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        PagedResult<TimelineStreamEntryResponse>? page = await response.Content
            .ReadFromJsonAsync<PagedResult<TimelineStreamEntryResponse>>(TestContext.Current.CancellationToken);
        TimelineStreamEntryResponse item = page!.Items[0];

        item.Origin.ShouldBe(TimelineEntryOrigin.External);
        item.SourceKey.ShouldBe("auditing");
        item.SourceId.ShouldBe("audit-42");
        item.EditedAt.ShouldBeNull();
    }

    [Fact]
    public async Task GetStream_emits_native_source_key_for_native_entries()
    {
        var entry = new TimelineStreamEntry
        {
            Id = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            EntryType = TimelineStreamEntryType.Comment,
            AuthorId = "user-1",
            AuthorName = "Alice",
            Body = "Hello",
        };

        _reader.GetStreamAsync("Patient", "42", 1, QueryEngineDefaults.DefaultPageSize, Arg.Any<CancellationToken>())
            .Returns(new TimelineStreamResult(new PagedResult<TimelineStreamEntry>([entry], 1, HasMore: false), []));

        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/Patient/42", TestContext.Current.CancellationToken);

        PagedResult<TimelineStreamEntryResponse>? page = await response.Content
            .ReadFromJsonAsync<PagedResult<TimelineStreamEntryResponse>>(TestContext.Current.CancellationToken);
        TimelineStreamEntryResponse item = page!.Items[0];

        item.Origin.ShouldBe(TimelineEntryOrigin.Native);
        // Native rows emit the reserved "native" key (never null) so the front can rely on a string.
        item.SourceKey.ShouldBe(TimelineSourceKeys.Native);
        item.SourceId.ShouldBeNull();
    }

    [Fact]
    public async Task GetStream_empty_returns_empty_page()
    {
        // Arrange
        _reader.GetStreamAsync("Invoice", "99", 1, QueryEngineDefaults.DefaultPageSize, Arg.Any<CancellationToken>())
            .Returns(new TimelineStreamResult(new PagedResult<TimelineStreamEntry>([], 0, HasMore: false), []));

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
            .Returns(new TimelineStreamResult(new PagedResult<TimelineStreamEntry>([], 50, HasMore: false), []));

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
    public async Task GetStream_without_permission_returns_403()
    {
        // Arrange — authenticated but lacking the Entries.Read permission
        HttpClient unauthorizedClient = BuildClient("Timeline.Unrelated.Read");

        // Act
        HttpResponseMessage response = await unauthorizedClient.GetAsync(
            $"{Prefix}/Patient/42", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // -- Helpers ---------------------------------------------------------------

    private HttpClient BuildClient(params string[] permissions)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, string.Join(',', permissions));
        return client;
    }
}
