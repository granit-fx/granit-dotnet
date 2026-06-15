using System.Net;
using System.Net.Http.Json;
using Granit.AI.Chat.Domain;
using Granit.AI.Chat.Endpoints.Dtos;
using Granit.AI.Chat.Endpoints.Extensions;
using Granit.AI.Chat.Endpoints.Permissions;
using Granit.AI.Chat.Exceptions;
using Granit.AI.Chat.Settings;
using Granit.AI.Exceptions;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.RateLimiting.Extensions;
using Granit.Testing.Endpoints;
using Granit.Users;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Granit.AI.Chat.Endpoints.Tests;

/// <summary>
/// HTTP-level tests for the chat endpoints (conversation CRUD, workspace listing, and the
/// SSE send path) driven through <see cref="GranitEndpointTestHost"/> with substituted services.
/// </summary>
public sealed class ChatEndpointsHttpTests
{
    private static readonly Guid Owner = Guid.Parse("0c0c0c0c-0c0c-0c0c-0c0c-0c0c0c0c0c0c");

    private readonly IConversationStore _store = Substitute.For<IConversationStore>();
    private readonly IChatService _chatService = Substitute.For<IChatService>();
    private readonly StubWorkspaceCatalog _catalog = new();
    private readonly IGuidGenerator _guids = Substitute.For<IGuidGenerator>();

    public ChatEndpointsHttpTests() => _guids.Create().Returns(_ => Guid.NewGuid());

    private Task<GranitEndpointTestHost> StartAsync(string? userId) =>
        GranitEndpointTestHost.StartAsync(
            configureServices: services =>
            {
                services.AddAuthorizationBuilder()
                    .AddPolicy(AIChatPermissions.Conversations.Read, p => p.RequireClaim(TestAuthHandler.PermissionClaimType, AIChatPermissions.Conversations.Read))
                    .AddPolicy(AIChatPermissions.Conversations.Send, p => p.RequireClaim(TestAuthHandler.PermissionClaimType, AIChatPermissions.Conversations.Send))
                    .AddPolicy(AIChatPermissions.Conversations.Manage, p => p.RequireClaim(TestAuthHandler.PermissionClaimType, AIChatPermissions.Conversations.Manage))
                    .AddPolicy(AIChatPermissions.Conversations.Delete, p => p.RequireClaim(TestAuthHandler.PermissionClaimType, AIChatPermissions.Conversations.Delete));

                services.AddSingleton(_store);
                services.AddSingleton(_chatService);
                services.AddSingleton<IChatWorkspaceCatalog>(_catalog);
                services.AddSingleton(_guids);
                services.AddSingleton<ICurrentUserService>(new StubCurrentUser(userId));

                // The send endpoint carries .RequireGranitRateLimiting; its filter resolves the
                // TenantPartitionedRateLimiter. With no "ai-chat-send" policy configured the limiter
                // no-ops (CheckAsync returns null), but the service must still be resolvable.
                services.AddMetrics();
                services.AddSingleton(TimeProvider.System);
                services.AddSingleton(Substitute.For<ICurrentTenant>());
                services.AddGranitRateLimiting();
            },
            configureEndpoints: app => app.MapGranitConversations());

    private static HttpClient FullAccess(GranitEndpointTestHost host) => host.CreateClientWithPermissions(
        AIChatPermissions.Conversations.Read,
        AIChatPermissions.Conversations.Send,
        AIChatPermissions.Conversations.Manage,
        AIChatPermissions.Conversations.Delete);

    // ── Conversation CRUD ───────────────────────────────────────────────────────

    [Fact]
    public async Task List_without_a_user_context_is_unauthorized()
    {
        await using GranitEndpointTestHost host = await StartAsync(userId: null);

        HttpResponseMessage response = await FullAccess(host).GetAsync("/conversations", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_without_the_read_permission_is_forbidden()
    {
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpClient client = host.CreateClientWithPermissions("AIChat.Conversations.Other");
        HttpResponseMessage response = await client.GetAsync("/conversations", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_returns_the_callers_conversations()
    {
        _store.ListAsync(Owner, Arg.Any<CancellationToken>()).Returns([Conversation.Create(Guid.NewGuid(), Owner, "First")]);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        List<ConversationSummaryResponse>? body = await FullAccess(host)
            .GetFromJsonAsync<List<ConversationSummaryResponse>>("/conversations", TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetById_returns_404_when_absent()
    {
        var id = Guid.NewGuid();
        _store.GetAsync(id, Owner, Arg.Any<CancellationToken>()).Returns((Conversation?)null);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).GetAsync($"/conversations/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_returns_the_conversation_when_found()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Owner, "Found");
        _store.GetAsync(conversation.Id, Owner, Arg.Any<CancellationToken>()).Returns(conversation);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).GetAsync($"/conversations/{conversation.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_persists_and_returns_201()
    {
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PostAsJsonAsync(
            "/conversations", new CreateConversationRequest("New chat"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        await _store.Received(1).CreateAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rename_returns_204_when_renamed()
    {
        var id = Guid.NewGuid();
        _store.RenameAsync(id, Owner, "Renamed", Arg.Any<CancellationToken>()).Returns(true);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PutAsJsonAsync(
            $"/conversations/{id}/title", new RenameConversationRequest("Renamed"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Rename_returns_404_when_absent()
    {
        var id = Guid.NewGuid();
        _store.RenameAsync(id, Owner, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PutAsJsonAsync(
            $"/conversations/{id}/title", new RenameConversationRequest("Renamed"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_returns_204_when_deleted()
    {
        var id = Guid.NewGuid();
        _store.DeleteAsync(id, Owner, Arg.Any<CancellationToken>()).Returns(true);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).DeleteAsync($"/conversations/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_returns_404_when_absent()
    {
        var id = Guid.NewGuid();
        _store.DeleteAsync(id, Owner, Arg.Any<CancellationToken>()).Returns(false);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).DeleteAsync($"/conversations/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── Workspaces ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Workspaces_returns_the_selectable_catalogue()
    {
        _catalog.Workspaces = ["Auto", "Support"];
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        ChatWorkspacesResponse? body = await FullAccess(host)
            .GetFromJsonAsync<ChatWorkspacesResponse>("/conversations/workspaces", TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Workspaces.ShouldBe(["Auto", "Support"]);
    }

    // ── Send (SSE) ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Send_without_a_user_context_is_unauthorized()
    {
        await using GranitEndpointTestHost host = await StartAsync(userId: null);

        HttpResponseMessage response = await FullAccess(host).PostAsJsonAsync(
            "/conversations/messages", new SendMessageRequest("Hello"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Send_maps_a_non_chat_capable_workspace_to_422()
    {
        _chatService.SendAsync(Arg.Any<ChatSendRequest>(), Arg.Any<CancellationToken>())
            .Returns<ChatSendResult>(_ => throw new WorkspaceNotChatCapableException("vectors"));
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PostAsJsonAsync(
            "/conversations/messages", new SendMessageRequest("Hi", WorkspaceName: "vectors"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Send_maps_an_unknown_conversation_to_404()
    {
        var id = Guid.NewGuid();
        _chatService.SendAsync(Arg.Any<ChatSendRequest>(), Arg.Any<CancellationToken>())
            .Returns<ChatSendResult>(_ => throw new ConversationNotFoundException(id));
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PostAsJsonAsync(
            "/conversations/messages", new SendMessageRequest("Hi", ConversationId: id), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Send_maps_an_unknown_workspace_to_404()
    {
        _chatService.SendAsync(Arg.Any<ChatSendRequest>(), Arg.Any<CancellationToken>())
            .Returns<ChatSendResult>(_ => throw new AIWorkspaceNotFoundException("ghost"));
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PostAsJsonAsync(
            "/conversations/messages", new SendMessageRequest("Hi", WorkspaceName: "ghost"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Send_streams_the_answer_as_server_sent_events()
    {
        var conversationId = Guid.NewGuid();
        _chatService.SendAsync(Arg.Any<ChatSendRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ChatSendResult
            {
                ConversationId = conversationId,
                Content = "Hello world",
                InputTokens = 3,
                OutputTokens = 2,
            });
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PostAsJsonAsync(
            "/conversations/messages", new SendMessageRequest("Hi"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("conversation");
        body.ShouldContain("delta");
        body.ShouldContain("Hello");
        body.ShouldContain("usage");
    }

    private sealed class StubWorkspaceCatalog : IChatWorkspaceCatalog
    {
        public IReadOnlyList<string> Workspaces { get; set; } = [];

        public ValueTask<IReadOnlyList<string>> GetSelectableWorkspacesAsync(CancellationToken cancellationToken = default) =>
            new(Workspaces);
    }

    private sealed class StubCurrentUser(string? userId) : ICurrentUserService
    {
        public string? UserId => userId;
        public string? UserName => null;
        public string? Email => null;
        public string? FirstName => null;
        public string? LastName => null;
        public bool IsAuthenticated => userId is not null;
        public IReadOnlyList<string> GetRoles() => [];
        public bool IsInRole(string role) => false;
    }
}
