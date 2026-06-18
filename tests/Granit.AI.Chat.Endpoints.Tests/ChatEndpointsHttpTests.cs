using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Granit.AI.Chat.Clarification;
using Granit.AI.Chat.Domain;
using Granit.AI.Chat.Endpoints.Dtos;
using Granit.AI.Chat.Endpoints.Extensions;
using Granit.AI.Chat.Endpoints.Permissions;
using Granit.AI.Chat.Exceptions;
using Granit.AI.Chat.Settings;
using Granit.AI.Exceptions;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.QueryEngine;
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
                    .AddPolicy(AIChatPermissions.Conversations.Delete, p => p.RequireClaim(TestAuthHandler.PermissionClaimType, AIChatPermissions.Conversations.Delete))
                    .AddPolicy(AIChatPermissions.Conversations.Report, p => p.RequireClaim(TestAuthHandler.PermissionClaimType, AIChatPermissions.Conversations.Report));

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
        AIChatPermissions.Conversations.Delete,
        AIChatPermissions.Conversations.Report);

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
        _store.GetMetadataAsync(id, Owner, Arg.Any<CancellationToken>()).Returns((Conversation?)null);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).GetAsync($"/conversations/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_returns_the_conversation_when_found()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Owner, "Found");
        _store.GetMetadataAsync(conversation.Id, Owner, Arg.Any<CancellationToken>()).Returns(conversation);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).GetAsync($"/conversations/{conversation.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── Messages (backwards keyset pagination) ───────────────────────────────────

    [Fact]
    public async Task Messages_without_a_user_context_is_unauthorized()
    {
        await using GranitEndpointTestHost host = await StartAsync(userId: null);

        HttpResponseMessage response = await FullAccess(host)
            .GetAsync($"/conversations/{Guid.NewGuid()}/messages", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Messages_returns_404_for_a_conversation_that_is_not_the_callers()
    {
        var id = Guid.NewGuid();
        _store.GetMessagesPageAsync(id, Owner, Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns((PagedResult<Message>?)null);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host)
            .GetAsync($"/conversations/{id}/messages", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Messages_returns_the_newest_page_first_with_a_cursor()
    {
        var id = Guid.NewGuid();
        var newer = Message.Create(Guid.NewGuid(), id, MessageRole.Assistant, "newer");
        var older = Message.Create(Guid.NewGuid(), id, MessageRole.User, "older");
        _store.GetMessagesPageAsync(id, Owner, null, Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Message>([newer, older], TotalCount: null, HasMore: true, NextCursor: "older-cursor"));

        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        PagedResult<MessageResponse>? body = await FullAccess(host)
            .GetFromJsonAsync<PagedResult<MessageResponse>>($"/conversations/{id}/messages", TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Items.Select(m => m.Content).ShouldBe(["newer", "older"]);
        body.Items[0].Role.ShouldBe("assistant");
        body.TotalCount.ShouldBeNull();
        body.NextCursor.ShouldBe("older-cursor");
    }

    [Fact]
    public async Task Messages_passes_the_cursor_and_pageSize_through_for_older_pages()
    {
        var id = Guid.NewGuid();
        _store.GetMessagesPageAsync(id, Owner, "older-cursor", 10, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Message>([], TotalCount: null, HasMore: false, NextCursor: null));

        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host)
            .GetAsync($"/conversations/{id}/messages?cursor=older-cursor&pageSize=10", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        // The cursor and page size flow straight through to the owner-scoped store query.
        await _store.Received(1).GetMessagesPageAsync(id, Owner, "older-cursor", 10, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Messages_without_the_read_permission_is_forbidden()
    {
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpClient client = host.CreateClientWithPermissions("AIChat.Conversations.Other");
        HttpResponseMessage response = await client.GetAsync(
            $"/conversations/{Guid.NewGuid()}/messages", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
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
    public async Task SetFavorite_returns_204_when_updated()
    {
        var id = Guid.NewGuid();
        _store.SetFavoriteAsync(id, Owner, true, Arg.Any<CancellationToken>()).Returns(true);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PutAsJsonAsync(
            $"/conversations/{id}/favorite", new SetConversationFavoriteRequest(true), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _store.Received(1).SetFavoriteAsync(id, Owner, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetFavorite_passes_the_explicit_false_state_through()
    {
        var id = Guid.NewGuid();
        _store.SetFavoriteAsync(id, Owner, false, Arg.Any<CancellationToken>()).Returns(true);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PutAsJsonAsync(
            $"/conversations/{id}/favorite", new SetConversationFavoriteRequest(false), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _store.Received(1).SetFavoriteAsync(id, Owner, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetFavorite_returns_404_when_absent()
    {
        var id = Guid.NewGuid();
        _store.SetFavoriteAsync(id, Owner, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(false);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PutAsJsonAsync(
            $"/conversations/{id}/favorite", new SetConversationFavoriteRequest(true), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SetFavorite_without_the_manage_permission_is_forbidden()
    {
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpClient client = host.CreateClientWithPermissions(AIChatPermissions.Conversations.Read);
        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/conversations/{Guid.NewGuid()}/favorite", new SetConversationFavoriteRequest(true), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SetFavorite_without_a_user_context_is_unauthorized()
    {
        await using GranitEndpointTestHost host = await StartAsync(userId: null);

        HttpResponseMessage response = await FullAccess(host).PutAsJsonAsync(
            $"/conversations/{Guid.NewGuid()}/favorite", new SetConversationFavoriteRequest(true), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
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

    // ── Report a message ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Report_returns_202_when_recorded()
    {
        var messageId = Guid.NewGuid();
        _store.ReportMessageAsync(
                Arg.Any<Guid>(), messageId, Owner, "Wrong answer", MessageReportCategory.Inaccurate, Arg.Any<CancellationToken>())
            .Returns(true);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PostAsJsonAsync(
            $"/conversations/messages/{messageId}/report",
            new ReportMessageRequest("Wrong answer", MessageReportCategory.Inaccurate),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await _store.Received(1).ReportMessageAsync(
            Arg.Any<Guid>(), messageId, Owner, "Wrong answer", MessageReportCategory.Inaccurate, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Report_returns_404_when_the_message_is_not_the_callers()
    {
        var messageId = Guid.NewGuid();
        _store.ReportMessageAsync(
                Arg.Any<Guid>(), messageId, Owner, Arg.Any<string>(), Arg.Any<MessageReportCategory?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PostAsJsonAsync(
            $"/conversations/messages/{messageId}/report",
            new ReportMessageRequest("Probing"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Report_without_a_user_context_is_unauthorized()
    {
        await using GranitEndpointTestHost host = await StartAsync(userId: null);

        HttpResponseMessage response = await FullAccess(host).PostAsJsonAsync(
            $"/conversations/messages/{Guid.NewGuid()}/report",
            new ReportMessageRequest("Wrong answer"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Report_without_the_report_permission_is_forbidden()
    {
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpClient client = host.CreateClientWithPermissions(AIChatPermissions.Conversations.Read);
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/conversations/messages/{Guid.NewGuid()}/report",
            new ReportMessageRequest("Wrong answer"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
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
    public async Task Send_maps_a_non_chat_capable_workspace_to_422_before_streaming()
    {
        _chatService.PrepareAsync(Arg.Any<ChatSendRequest>(), Arg.Any<CancellationToken>())
            .Returns<ChatSendHandle>(_ => throw new WorkspaceNotChatCapableException("vectors"));
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PostAsJsonAsync(
            "/conversations/messages", new SendMessageRequest("Hi", WorkspaceName: "vectors"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        // The failure is returned as a plain problem, not an opened SSE stream.
        response.Content.Headers.ContentType!.MediaType.ShouldNotBe("text/event-stream");
    }

    [Fact]
    public async Task Send_maps_an_unknown_conversation_to_404_before_streaming()
    {
        var id = Guid.NewGuid();
        _chatService.PrepareAsync(Arg.Any<ChatSendRequest>(), Arg.Any<CancellationToken>())
            .Returns<ChatSendHandle>(_ => throw new ConversationNotFoundException(id));
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PostAsJsonAsync(
            "/conversations/messages", new SendMessageRequest("Hi", ConversationId: id), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldNotBe("text/event-stream");
    }

    [Fact]
    public async Task Send_maps_an_unknown_workspace_to_404_before_streaming()
    {
        _chatService.PrepareAsync(Arg.Any<ChatSendRequest>(), Arg.Any<CancellationToken>())
            .Returns<ChatSendHandle>(_ => throw new AIWorkspaceNotFoundException("ghost"));
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PostAsJsonAsync(
            "/conversations/messages", new SendMessageRequest("Hi", WorkspaceName: "ghost"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldNotBe("text/event-stream");
    }

    [Fact]
    public async Task Send_streams_the_answer_as_server_sent_events()
    {
        var conversationId = Guid.NewGuid();
        _chatService.PrepareAsync(Arg.Any<ChatSendRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ChatSendHandle { ConversationId = conversationId });
        _chatService.StreamAsync(Arg.Any<ChatSendHandle>(), Arg.Any<CancellationToken>())
            .Returns(_ => Stream(
                ChatTurnUpdate.ForDelta("Hello world"),
                ChatTurnUpdate.ForCompleted(new ChatSendResult
                {
                    ConversationId = conversationId,
                    Content = "Hello world",
                    InputTokens = 3,
                    OutputTokens = 2,
                })));
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

    [Fact]
    public async Task Send_streams_tool_call_and_tool_result_frames()
    {
        var conversationId = Guid.NewGuid();
        _chatService.PrepareAsync(Arg.Any<ChatSendRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ChatSendHandle { ConversationId = conversationId });
        _chatService.StreamAsync(Arg.Any<ChatSendHandle>(), Arg.Any<CancellationToken>())
            .Returns(_ => Stream(
                ChatTurnUpdate.ForToolCall("query_data", "call-1"),
                ChatTurnUpdate.ForToolResult("query_data", "call-1", true),
                ChatTurnUpdate.ForDelta("Done"),
                ChatTurnUpdate.ForCompleted(new ChatSendResult { ConversationId = conversationId, Content = "Done" })));
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PostAsJsonAsync(
            "/conversations/messages", new SendMessageRequest("Hi"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("tool_call");
        body.ShouldContain("tool_result");
        body.ShouldContain("query_data");
    }

    [Fact]
    public async Task Send_emits_one_persisted_frame_with_the_turns_real_messages_before_usage()
    {
        var conversationId = Guid.NewGuid();
        var userMessageId = Guid.NewGuid();
        var assistantMessageId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 6, 18, 9, 30, 0, TimeSpan.Zero);
        _chatService.PrepareAsync(Arg.Any<ChatSendRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ChatSendHandle { ConversationId = conversationId });
        _chatService.StreamAsync(Arg.Any<ChatSendHandle>(), Arg.Any<CancellationToken>())
            .Returns(_ => Stream(
                ChatTurnUpdate.ForDelta("Hello world"),
                ChatTurnUpdate.ForCompleted(new ChatSendResult
                {
                    ConversationId = conversationId,
                    Content = "Hello world",
                    InputTokens = 3,
                    OutputTokens = 2,
                    PersistedMessages =
                    [
                        new PersistedChatMessage(userMessageId, "user", "Hi", createdAt),
                        new PersistedChatMessage(assistantMessageId, "assistant", "Hello world", createdAt),
                    ],
                })));
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PostAsJsonAsync(
            "/conversations/messages", new SendMessageRequest("Hi"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Exactly one persisted frame, carrying both real message ids, ordered before usage.
        int persistedAt = body.IndexOf("\"type\":\"persisted\"", StringComparison.Ordinal);
        persistedAt.ShouldBeGreaterThanOrEqualTo(0);
        body.IndexOf("\"type\":\"persisted\"", persistedAt + 1, StringComparison.Ordinal).ShouldBe(-1);
        persistedAt.ShouldBeLessThan(body.IndexOf("\"type\":\"usage\"", StringComparison.Ordinal));
        body.ShouldContain(userMessageId.ToString());
        body.ShouldContain(assistantMessageId.ToString());
        // The camelCase MessageResponse shape the GET endpoint already uses is reused verbatim.
        body.ShouldContain("\"role\":\"assistant\"");
        body.ShouldContain("\"createdAt\":");
    }

    [Fact]
    public async Task Send_emits_no_persisted_frame_on_a_clarification_turn()
    {
        var conversationId = Guid.NewGuid();
        _chatService.PrepareAsync(Arg.Any<ChatSendRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ChatSendHandle { ConversationId = conversationId });
        // The service persists the question as an assistant row, so the identities ride along — but the
        // endpoint must suppress the frame: there is no answer bubble to append, only clickable options.
        _chatService.StreamAsync(Arg.Any<ChatSendHandle>(), Arg.Any<CancellationToken>())
            .Returns(_ => Stream(
                ChatTurnUpdate.ForCompleted(new ChatSendResult
                {
                    ConversationId = conversationId,
                    Content = "Which environment?",
                    Clarification = new AIClarificationRequest
                    {
                        Question = "Which environment?",
                        Options = [new AIClarificationOption { Label = "Prod" }, new AIClarificationOption { Label = "Test" }],
                        AllowOther = false,
                    },
                    PersistedMessages =
                    [
                        new PersistedChatMessage(Guid.NewGuid(), "user", "deploy", default),
                        new PersistedChatMessage(Guid.NewGuid(), "assistant", "Which environment?", default),
                    ],
                })));
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PostAsJsonAsync(
            "/conversations/messages", new SendMessageRequest("deploy"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("clarification");
        body.ShouldNotContain("\"type\":\"persisted\"");
    }

    [Fact]
    public async Task Send_flushes_the_conversation_frame_before_the_agent_settles()
    {
        var conversationId = Guid.NewGuid();
        var gate = new TaskCompletionSource();
        _chatService.PrepareAsync(Arg.Any<ChatSendRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ChatSendHandle { ConversationId = conversationId });
        // The agent is gated open: StreamAsync never produces a frame until the test releases it.
        _chatService.StreamAsync(Arg.Any<ChatSendHandle>(), Arg.Any<CancellationToken>())
            .Returns(_ => GatedStream(gate.Task, conversationId));
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/conversations/messages")
            {
                Content = JsonContent.Create(new SendMessageRequest("Hi")),
            };

            // Headers (and the conversation frame that triggers them) must arrive while the agent is
            // still blocked — the whole point of the early flush. ResponseHeadersRead returns as soon
            // as they do; if the pipeline were buffered this would hang until the gate is released.
            using HttpResponseMessage response = await FullAccess(host).SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");
        }
        finally
        {
            gate.SetResult();
        }
    }

    [Fact]
    public async Task Send_ends_with_a_rate_limit_error_frame_when_the_provider_is_throttled_mid_stream()
    {
        var conversationId = Guid.NewGuid();
        _chatService.PrepareAsync(Arg.Any<ChatSendRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ChatSendHandle { ConversationId = conversationId });
        // The provider returns 429 part-way through the stream — a "denial of wallet" mid-flight.
        _chatService.StreamAsync(Arg.Any<ChatSendHandle>(), Arg.Any<CancellationToken>())
            .Returns(_ => ThrowingStream(
                new HttpRequestException("secret-provider-detail", null, HttpStatusCode.TooManyRequests)));
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PostAsJsonAsync(
            "/conversations/messages", new SendMessageRequest("Hi"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("\"type\":\"error\"");
        body.ShouldContain("rate_limit");
        // The raw provider detail never reaches the wire — only the machine code does.
        body.ShouldNotContain("secret-provider-detail");
    }

    [Fact]
    public async Task Send_ends_with_a_provider_unavailable_error_frame_on_a_provider_5xx()
    {
        var conversationId = Guid.NewGuid();
        _chatService.PrepareAsync(Arg.Any<ChatSendRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ChatSendHandle { ConversationId = conversationId });
        _chatService.StreamAsync(Arg.Any<ChatSendHandle>(), Arg.Any<CancellationToken>())
            .Returns(_ => ThrowingStream(
                new HttpRequestException("upstream down", null, HttpStatusCode.ServiceUnavailable)));
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PostAsJsonAsync(
            "/conversations/messages", new SendMessageRequest("Hi"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("provider_unavailable");
    }

    [Fact]
    public async Task Send_ends_with_a_server_error_frame_for_an_unclassified_failure()
    {
        var conversationId = Guid.NewGuid();
        _chatService.PrepareAsync(Arg.Any<ChatSendRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ChatSendHandle { ConversationId = conversationId });
        _chatService.StreamAsync(Arg.Any<ChatSendHandle>(), Arg.Any<CancellationToken>())
            .Returns(_ => ThrowingStream(new InvalidOperationException("boom")));
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PostAsJsonAsync(
            "/conversations/messages", new SendMessageRequest("Hi"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("\"type\":\"error\"");
        body.ShouldContain("server_error");
        // A failed turn settles no result, so no persisted identities are emitted.
        body.ShouldNotContain("\"type\":\"persisted\"");
    }

    [Fact]
    public async Task Send_does_not_convert_a_client_cancellation_into_an_error_frame()
    {
        var conversationId = Guid.NewGuid();
        _chatService.PrepareAsync(Arg.Any<ChatSendRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ChatSendHandle { ConversationId = conversationId });
        // The stream blocks until the request is cancelled, then surfaces an OperationCanceledException
        // with the request token set — the client-abort case the endpoint must not turn into an error.
        _chatService.StreamAsync(Arg.Any<ChatSendHandle>(), Arg.Any<CancellationToken>())
            .Returns(ci => BlockingStream(ci.Arg<CancellationToken>()));
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        using var cts = new CancellationTokenSource();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/conversations/messages")
        {
            Content = JsonContent.Create(new SendMessageRequest("Hi")),
        };
        using HttpResponseMessage response = await FullAccess(host).SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var seen = new System.Text.StringBuilder();
        try
        {
            await using Stream stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var reader = new StreamReader(stream);
            char[] buffer = new char[64];
            int read;
            while ((read = await reader.ReadAsync(buffer, cts.Token)) > 0)
            {
                seen.Append(buffer, 0, read);
                // The conversation frame proves the stream committed; now cancel like a closing client.
                if (seen.ToString().Contains("conversation"))
                {
                    await cts.CancelAsync();
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException)
        {
            // Expected: the cancellation aborts the stream rather than producing an error frame.
        }

        seen.ToString().ShouldContain("conversation");
        seen.ToString().ShouldNotContain("error");
    }

    private static async IAsyncEnumerable<ChatTurnUpdate> Stream(params ChatTurnUpdate[] updates)
    {
        foreach (ChatTurnUpdate update in updates)
        {
            await Task.Yield();
            yield return update;
        }
    }

    private static async IAsyncEnumerable<ChatTurnUpdate> ThrowingStream(Exception error)
    {
        await Task.Yield();
        yield return ChatTurnUpdate.ForDelta("partial");
        throw error;
    }

    private static async IAsyncEnumerable<ChatTurnUpdate> BlockingStream(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // The endpoint emits the conversation frame itself; this never yields and blocks until the
        // request is cancelled, at which point Task.Delay throws with the token set.
        await Task.Delay(Timeout.Infinite, cancellationToken);
        yield break;
    }

    private static async IAsyncEnumerable<ChatTurnUpdate> GatedStream(
        Task gate,
        Guid conversationId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        yield return ChatTurnUpdate.ForCompleted(new ChatSendResult { ConversationId = conversationId, Content = string.Empty });
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
