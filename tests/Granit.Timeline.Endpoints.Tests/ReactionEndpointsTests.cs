using System.Net;
using Granit.Domain;
using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Domain;
using Granit.Timeline.Domain.ValueObjects;
using Granit.Timeline.Endpoints.Extensions;
using Granit.Timeline.Endpoints.Permissions;
using Granit.Timing;
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
/// Integration tests for the POST reaction-toggle endpoint, focused on the
/// author notification wired after a reaction is added.
/// </summary>
public sealed class ReactionEndpointsTests : IAsyncDisposable
{
    private const string Prefix = "/timeline";
    private static readonly Guid s_reactingUserId = Guid.NewGuid();

    private readonly IReactionReader _reactionReader = Substitute.For<IReactionReader>();
    private readonly IReactionWriter _reactionWriter = Substitute.For<IReactionWriter>();
    private readonly ITimelineReader _entryReader = Substitute.For<ITimelineReader>();
    private readonly ITimelineNotifier _notifier = Substitute.For<ITimelineNotifier>();
    private readonly ILocalEventBus _eventBus = Substitute.For<ILocalEventBus>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly WebApplication _app;
    private readonly HttpClient _authClient;

    public ReactionEndpointsTests()
    {
        _currentUser.UserId.Returns(s_reactingUserId.ToString());
        _currentTenant.IsAvailable.Returns(false);
        _clock.Now.Returns(DateTimeOffset.UtcNow);
        _clock.Normalize(Arg.Any<DateTimeOffset>()).Returns(callInfo => callInfo.Arg<DateTimeOffset>());
        _guidGenerator.Create().Returns(Guid.NewGuid());

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(TimelinePermissions.Entries.Read, policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TimelinePermissions.Entries.Read))
            .AddPolicy(TimelinePermissions.Reactions.React, policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TimelinePermissions.Reactions.React));

        builder.Services.AddSingleton(_reactionReader);
        builder.Services.AddSingleton(_reactionWriter);
        builder.Services.AddSingleton(_entryReader);
        builder.Services.AddSingleton(_notifier);
        builder.Services.AddSingleton(_eventBus);
        builder.Services.AddSingleton(_currentUser);
        builder.Services.AddSingleton(_currentTenant);
        builder.Services.AddSingleton(_guidGenerator);
        builder.Services.AddSingleton(_clock);

        _app = builder.Build();
        _app.MapGranitTimeline();
        _app.StartAsync().GetAwaiter().GetResult();

        _authClient = BuildClient(TimelinePermissions.Entries.Read, TimelinePermissions.Reactions.React);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task ToggleReaction_added_notifies_author_when_reactor_is_not_author()
    {
        // Arrange
        var entryId = Guid.NewGuid();
        var entry = TimelineEntry.Create(
            entryId, new EntityReference("Patient", "42"), TimelineEntryType.Comment,
            "Their comment", new AuthorInfo("author-1", "Author"), DateTimeOffset.UtcNow, "author-1");

        _reactionReader.FindAsync(entryId, s_reactingUserId, "👍", Arg.Any<CancellationToken>())
            .Returns((Reaction?)null);
        _reactionReader.GetByEntryAsync(entryId, Arg.Any<CancellationToken>())
            .Returns([]);
        _entryReader.GetByIdAsync(entryId, Arg.Any<CancellationToken>())
            .Returns(entry);

        // Act
        HttpResponseMessage response = await _authClient.PostAsync(
            $"{Prefix}/entries/{entryId}/reactions/%F0%9F%91%8D", content: null, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _notifier.Received(1).NotifyReactionToggledAsync(
            entry, s_reactingUserId.ToString(), "👍", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleReaction_removed_does_not_notify_author()
    {
        // Arrange
        var entryId = Guid.NewGuid();
        var existing = Reaction.Create(
            Guid.NewGuid(), entryId, s_reactingUserId, "👍", DateTimeOffset.UtcNow, s_reactingUserId.ToString());

        _reactionReader.FindAsync(entryId, s_reactingUserId, "👍", Arg.Any<CancellationToken>())
            .Returns(existing);
        _reactionReader.GetByEntryAsync(entryId, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        HttpResponseMessage response = await _authClient.PostAsync(
            $"{Prefix}/entries/{entryId}/reactions/%F0%9F%91%8D", content: null, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _entryReader.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _notifier.DidNotReceive().NotifyReactionToggledAsync(
            Arg.Any<TimelineEntry>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // -- Helpers ---------------------------------------------------------------

    private HttpClient BuildClient(params string[] permissions)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, string.Join(',', permissions));
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, s_reactingUserId.ToString());
        return client;
    }
}
