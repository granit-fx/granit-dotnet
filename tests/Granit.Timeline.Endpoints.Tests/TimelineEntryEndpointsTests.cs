using System.Net;
using System.Net.Http.Json;
using Granit.Security;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Domain;
using Granit.Timeline.Endpoints.Dtos;
using Granit.Timeline.Endpoints.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Endpoints.Tests;

/// <summary>
/// Integration tests for the POST/DELETE entry endpoints.
/// </summary>
public sealed class TimelineEntryEndpointsTests : IAsyncDisposable
{
    private const string UserRole = "granit-timeline-user";
    private const string Prefix = "/timeline";

    private readonly ITimelineWriter _writer = Substitute.For<ITimelineWriter>();
    private readonly ITimelineFollowerService _followerService = Substitute.For<ITimelineFollowerService>();
    private readonly ITimelineNotifier _notifier = Substitute.For<ITimelineNotifier>();
    private readonly WebApplication _app;
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    public TimelineEntryEndpointsTests()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(_writer);
        builder.Services.AddSingleton(_followerService);
        builder.Services.AddSingleton(_notifier);
        builder.Services.AddSingleton(Substitute.For<ICurrentUserService>());

        // Required by stream endpoints but not exercised here
        builder.Services.AddSingleton(Substitute.For<ITimelineReader>());

        _app = builder.Build();
        _app.MapTimelineEndpoints();
        _app.StartAsync().GetAwaiter().GetResult();

        _authClient = BuildClient(UserRole);
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // -- POST /{entityType}/{entityId}/entries ---------------------------------

    [Fact]
    public async Task PostEntry_valid_comment_returns_201()
    {
        // Arrange
        var entryId = Guid.NewGuid();
        var entry = TimelineEntry.Create(
            entryId, "Patient", "42", TimelineEntryType.Comment,
            "Test comment", "user-1", "Alice", DateTimeOffset.UtcNow, "user-1");

        _writer.PostEntryAsync("Patient", "42", TimelineEntryType.Comment, "Test comment",
                null, Arg.Any<CancellationToken>())
            .Returns(entry);

        _followerService.GetFollowerIdsAsync("Patient", "42", Arg.Any<CancellationToken>())
            .Returns(["user-1"]);

        PostTimelineEntryRequest request = new()
        {
            EntryType = TimelineEntryType.Comment,
            Body = "Test comment",
        };

        // Act
        HttpResponseMessage response = await _authClient.PostAsJsonAsync(
            $"{Prefix}/Patient/42/entries", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        TimelineStreamEntry? result = await response.Content
            .ReadFromJsonAsync<TimelineStreamEntry>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.Id.ShouldBe(entryId);
        result.Body.ShouldBe("Test comment");
        result.EntryType.ShouldBe(TimelineStreamEntryType.Comment);
    }

    [Fact]
    public async Task PostEntry_internal_note_returns_201_with_correct_type()
    {
        // Arrange
        var entryId = Guid.NewGuid();
        var entry = TimelineEntry.Create(
            entryId, "Patient", "42", TimelineEntryType.InternalNote,
            "Staff only note", "user-1", "Alice", DateTimeOffset.UtcNow, "user-1");

        _writer.PostEntryAsync("Patient", "42", TimelineEntryType.InternalNote, "Staff only note",
                null, Arg.Any<CancellationToken>())
            .Returns(entry);

        _followerService.GetFollowerIdsAsync("Patient", "42", Arg.Any<CancellationToken>())
            .Returns([]);

        PostTimelineEntryRequest request = new()
        {
            EntryType = TimelineEntryType.InternalNote,
            Body = "Staff only note",
        };

        // Act
        HttpResponseMessage response = await _authClient.PostAsJsonAsync(
            $"{Prefix}/Patient/42/entries", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        TimelineStreamEntry? result = await response.Content
            .ReadFromJsonAsync<TimelineStreamEntry>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.EntryType.ShouldBe(TimelineStreamEntryType.InternalNote);
    }

    [Fact]
    public async Task PostEntry_with_parent_id_delegates_correctly()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var entry = TimelineEntry.Create(
            entryId, "Patient", "42", TimelineEntryType.Comment,
            "Reply", "user-1", "Alice", DateTimeOffset.UtcNow, "user-1",
            parentEntryId: parentId);

        _writer.PostEntryAsync("Patient", "42", TimelineEntryType.Comment, "Reply",
                parentId, Arg.Any<CancellationToken>())
            .Returns(entry);

        _followerService.GetFollowerIdsAsync("Patient", "42", Arg.Any<CancellationToken>())
            .Returns([]);

        PostTimelineEntryRequest request = new()
        {
            EntryType = TimelineEntryType.Comment,
            Body = "Reply",
            ParentEntryId = parentId,
        };

        // Act
        HttpResponseMessage response = await _authClient.PostAsJsonAsync(
            $"{Prefix}/Patient/42/entries", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        TimelineStreamEntry? result = await response.Content
            .ReadFromJsonAsync<TimelineStreamEntry>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.ParentEntryId.ShouldBe(parentId);
    }

    [Fact]
    public async Task PostEntry_with_mentions_auto_subscribes_and_notifies()
    {
        // Arrange
        var mentionedUserId = Guid.NewGuid();
        string mentionBody = $"Hey @[Bob](user:{mentionedUserId}), check this!";
        var entryId = Guid.NewGuid();
        var entry = TimelineEntry.Create(
            entryId, "Patient", "42", TimelineEntryType.Comment,
            mentionBody, "user-1", "Alice", DateTimeOffset.UtcNow, "user-1");

        _writer.PostEntryAsync("Patient", "42", TimelineEntryType.Comment, mentionBody,
                null, Arg.Any<CancellationToken>())
            .Returns(entry);

        _followerService.GetFollowerIdsAsync("Patient", "42", Arg.Any<CancellationToken>())
            .Returns(["user-1", mentionedUserId.ToString()]);

        PostTimelineEntryRequest request = new()
        {
            EntryType = TimelineEntryType.Comment,
            Body = mentionBody,
        };

        // Act
        HttpResponseMessage response = await _authClient.PostAsJsonAsync(
            $"{Prefix}/Patient/42/entries", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Verify auto-subscribe for mentioned user
        await _followerService.Received(1).FollowAsync(
            mentionedUserId.ToString(), "Patient", "42", Arg.Any<CancellationToken>());

        // Verify followers were notified
        await _notifier.Received(1).NotifyEntryPostedAsync(
            Arg.Any<TimelineEntry>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());

        // Verify mentioned users were notified separately
        await _notifier.Received(1).NotifyMentionedUsersAsync(
            Arg.Any<TimelineEntry>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostEntry_without_mentions_does_not_call_NotifyMentionedUsers()
    {
        // Arrange
        var entryId = Guid.NewGuid();
        var entry = TimelineEntry.Create(
            entryId, "Patient", "42", TimelineEntryType.Comment,
            "No mentions here", "user-1", "Alice", DateTimeOffset.UtcNow, "user-1");

        _writer.PostEntryAsync("Patient", "42", TimelineEntryType.Comment, "No mentions here",
                null, Arg.Any<CancellationToken>())
            .Returns(entry);

        _followerService.GetFollowerIdsAsync("Patient", "42", Arg.Any<CancellationToken>())
            .Returns([]);

        PostTimelineEntryRequest request = new()
        {
            EntryType = TimelineEntryType.Comment,
            Body = "No mentions here",
        };

        // Act
        await _authClient.PostAsJsonAsync(
            $"{Prefix}/Patient/42/entries", request, TestContext.Current.CancellationToken);

        // Assert -- no mention notification
        await _notifier.DidNotReceive().NotifyMentionedUsersAsync(
            Arg.Any<TimelineEntry>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostEntry_without_auth_returns_401()
    {
        // Arrange
        PostTimelineEntryRequest request = new()
        {
            EntryType = TimelineEntryType.Comment,
            Body = "Unauthorized",
        };

        // Act
        HttpResponseMessage response = await _anonClient.PostAsJsonAsync(
            $"{Prefix}/Patient/42/entries", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // -- DELETE /{entityType}/{entityId}/entries/{entryId} ---------------------

    [Fact]
    public async Task DeleteEntry_existing_returns_204()
    {
        // Arrange
        var entryId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await _authClient.DeleteAsync(
            $"{Prefix}/Patient/42/entries/{entryId}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _writer.Received(1).DeleteEntryAsync(entryId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteEntry_nonexistent_returns_404()
    {
        // Arrange
        var entryId = Guid.NewGuid();
        _writer.DeleteEntryAsync(entryId, Arg.Any<CancellationToken>())
            .Throws(new KeyNotFoundException($"Timeline entry '{entryId}' not found."));

        // Act
        HttpResponseMessage response = await _authClient.DeleteAsync(
            $"{Prefix}/Patient/42/entries/{entryId}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteEntry_without_auth_returns_401()
    {
        // Act
        var entryId = Guid.NewGuid();
        HttpResponseMessage response = await _anonClient.DeleteAsync(
            $"{Prefix}/Patient/42/entries/{entryId}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // -- Helpers ---------------------------------------------------------------

    private HttpClient BuildClient(string role)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }
}
