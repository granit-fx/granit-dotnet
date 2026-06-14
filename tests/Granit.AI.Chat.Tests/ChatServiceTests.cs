using Granit.AI.Chat.Domain;
using Granit.AI.Chat.Exceptions;
using Granit.AI.Chat.Internal;
using Granit.AI.Options;
using Granit.AI.Tools;
using Granit.AI.Workspaces;
using Granit.Guids;
using Microsoft.Extensions.AI;
using NSubstitute;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.AI.Chat.Tests;

public sealed class ChatServiceTests
{
    private static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly IConversationStore _store = Substitute.For<IConversationStore>();
    private readonly IAIToolOrchestrator _orchestrator = Substitute.For<IAIToolOrchestrator>();
    private readonly IAIWorkspaceProvider _workspaceProvider = Substitute.For<IAIWorkspaceProvider>();
    private readonly IAIWorkspaceCapabilityResolver _capabilityResolver = Substitute.For<IAIWorkspaceCapabilityResolver>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();

    private ChatService CreateService()
    {
        _guidGenerator.Create().Returns(_ => Guid.NewGuid());
        _workspaceProvider.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AIWorkspace { Name = "default", Provider = "OpenAI", Model = "gpt-4o" });
        _capabilityResolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AIModelCapabilities { Chat = true });
        _orchestrator.RunAsync(Arg.Any<AIOrchestrationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AIOrchestrationResult
            {
                Content = "the answer",
                Messages = [],
                Iterations = 1,
                MaxIterationsReached = false,
                ToolInvocations = [],
                InputTokens = 10,
                OutputTokens = 4,
            });

        return new ChatService(_store, _orchestrator, _workspaceProvider, _capabilityResolver, _guidGenerator,
            MsOptions.Create(new GranitAIOptions()));
    }

    private static ChatSendRequest Request(Guid? conversationId = null, string message = "hello") =>
        new() { ConversationId = conversationId, OwnerId = Owner, Message = message };

    [Fact]
    public async Task New_conversation_runs_the_loop_and_persists_user_and_assistant_messages()
    {
        ChatService service = CreateService();

        ChatSendResult result = await service.SendAsync(Request(message: "What changed?"), TestContext.Current.CancellationToken);

        result.Content.ShouldBe("the answer");
        result.InputTokens.ShouldBe(10);

        await _store.Received(1).CreateAsync(
            Arg.Is<Conversation>(c =>
                c.OwnerId == Owner
                && c.Messages.Count == 2
                && c.Messages[0].Role == MessageRole.User
                && c.Messages[1].Role == MessageRole.Assistant
                && c.Messages[1].Content == "the answer"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Existing_conversation_feeds_history_to_the_loop_and_appends_messages()
    {
        var conversationId = Guid.NewGuid();
        var existing = Conversation.Create(conversationId, Owner, "Earlier");
        existing.AddMessage(Guid.NewGuid(), MessageRole.User, "previous");
        _store.GetAsync(conversationId, Owner, Arg.Any<CancellationToken>()).Returns(existing);
        _store.AppendMessagesAsync(conversationId, Owner, Arg.Any<IReadOnlyList<Message>>(), Arg.Any<CancellationToken>())
            .Returns(true);
        ChatService service = CreateService();

        await service.SendAsync(Request(conversationId, "follow up"), TestContext.Current.CancellationToken);

        await _orchestrator.Received(1).RunAsync(
            Arg.Is<AIOrchestrationRequest>(r =>
                r.Messages.Count == 2
                && r.Messages[0].Role == ChatRole.User && r.Messages[0].Text == "previous"
                && r.Messages[1].Text == "follow up"),
            Arg.Any<CancellationToken>());
        await _store.Received(1).AppendMessagesAsync(conversationId, Owner,
            Arg.Is<IReadOnlyList<Message>>(m => m.Count == 2), Arg.Any<CancellationToken>());
        await _store.DidNotReceive().CreateAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_chat_capable_workspace_is_rejected_before_the_loop()
    {
        ChatService service = CreateService();
        _capabilityResolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AIModelCapabilities { Chat = false });

        await Should.ThrowAsync<WorkspaceNotChatCapableException>(
            () => service.SendAsync(Request(), TestContext.Current.CancellationToken));

        await _orchestrator.DidNotReceive().RunAsync(Arg.Any<AIOrchestrationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unknown_conversation_for_the_owner_throws()
    {
        var conversationId = Guid.NewGuid();
        _store.GetAsync(conversationId, Owner, Arg.Any<CancellationToken>()).Returns((Conversation?)null);
        ChatService service = CreateService();

        await Should.ThrowAsync<ConversationNotFoundException>(
            () => service.SendAsync(Request(conversationId), TestContext.Current.CancellationToken));
    }
}
