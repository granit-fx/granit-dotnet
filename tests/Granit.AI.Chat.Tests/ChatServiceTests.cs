using System.Runtime.CompilerServices;
using System.Text.Json;
using Granit.AI.Chat.Attachments;
using Granit.AI.Chat.Clarification;
using Granit.AI.Chat.Domain;
using Granit.AI.Chat.Exceptions;
using Granit.AI.Chat.Internal;
using Granit.AI.Chat.Mentions;
using Granit.AI.Chat.Settings;
using Granit.AI.Chat.Suggestions;
using Granit.AI.Options;
using Granit.AI.Tools;
using Granit.AI.Workspaces;
using Granit.Guids;
using Granit.Settings.Services;
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
    private readonly IAIMentionContextResolver _mentionContextResolver = Substitute.For<IAIMentionContextResolver>();
    private readonly IAIAttachmentTextResolver _attachmentTextResolver = Substitute.For<IAIAttachmentTextResolver>();
    private readonly IAISuggestionResolver _suggestionResolver = Substitute.For<IAISuggestionResolver>();
    private readonly IPromptBadgeResolver _promptBadgeResolver = Substitute.For<IPromptBadgeResolver>();
    private readonly ISettingProvider _settingProvider = Substitute.For<ISettingProvider>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();

    private static readonly AIOrchestrationResult DefaultResult = new()
    {
        Content = "the answer",
        Messages = [],
        Iterations = 1,
        MaxIterationsReached = false,
        ToolInvocations = [],
        InputTokens = 10,
        OutputTokens = 4,
    };

    private ChatService CreateService()
    {
        _guidGenerator.Create().Returns(_ => Guid.NewGuid());
        _workspaceProvider.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AIWorkspace { Name = "default", Provider = "OpenAI", Model = "gpt-4o" });
        _capabilityResolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AIModelCapabilities { Chat = true });
        StubLoop(DefaultResult);

        _mentionContextResolver.ResolveContextAsync(Arg.Any<IReadOnlyList<AIMention>>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _attachmentTextResolver.ResolveContextAsync(Arg.Any<IReadOnlyList<AIAttachment>>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _settingProvider.GetOrNullAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _suggestionResolver.ResolveAsync(Arg.Any<AISuggestionContext>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _store.AppendMessagesAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Message>>(), Arg.Any<CancellationToken>())
            .Returns(true);

        return new ChatService(_store, _orchestrator, _workspaceProvider, _capabilityResolver,
            _mentionContextResolver, _attachmentTextResolver, _suggestionResolver, _promptBadgeResolver,
            _settingProvider, _guidGenerator, MsOptions.Create(new GranitAIOptions { DefaultWorkspace = "default" }));
    }

    private void StubLoop(AIOrchestrationResult result) =>
        _orchestrator.RunStreamingAsync(Arg.Any<AIOrchestrationRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => ToStream(result));

    private static async IAsyncEnumerable<AIOrchestrationUpdate> ToStream(
        AIOrchestrationResult result,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(result.Content))
        {
            yield return AIOrchestrationUpdate.Delta(result.Content);
        }

        yield return AIOrchestrationUpdate.Completed(result);
        await Task.CompletedTask;
    }

    /// <summary>Prepares then drains the stream, returning the settled result the endpoint would surface.</summary>
    private static async Task<ChatSendResult> SendAsync(ChatService service, ChatSendRequest request, CancellationToken cancellationToken)
    {
        ChatSendHandle handle = await service.PrepareAsync(request, cancellationToken);
        ChatSendResult? result = null;
        await foreach (ChatTurnUpdate update in service.StreamAsync(handle, cancellationToken))
        {
            if (update.Kind == ChatTurnUpdateKind.Completed)
            {
                result = update.Result;
            }
        }

        return result!;
    }

    private static ChatSendRequest Request(
        Guid? conversationId = null,
        string message = "hello",
        IReadOnlyList<AIMention>? mentions = null,
        IReadOnlyList<AIAttachment>? attachments = null,
        IReadOnlyList<Guid>? promptRefs = null) =>
        new()
        {
            ConversationId = conversationId,
            OwnerId = Owner,
            Message = message,
            Mentions = mentions,
            Attachments = attachments,
            PromptRefs = promptRefs,
        };

    [Fact]
    public async Task New_conversation_runs_the_loop_and_persists_user_then_assistant_messages()
    {
        ChatService service = CreateService();

        ChatSendResult result = await SendAsync(service, Request(message: "What changed?"), TestContext.Current.CancellationToken);

        result.Content.ShouldBe("the answer");
        result.InputTokens.ShouldBe(10);

        // The user turn is persisted up front (conversation created with just the user message)...
        await _store.Received(1).CreateAsync(
            Arg.Is<Conversation>(c =>
                c.OwnerId == Owner
                && c.Messages.Count == 1
                && c.Messages[0].Role == MessageRole.User
                && c.Messages[0].Content == "What changed?"),
            Arg.Any<CancellationToken>());
        // ...and the assistant turn is appended once the stream settles.
        await _store.Received(1).AppendMessagesAsync(
            Arg.Any<Guid>(), Owner,
            Arg.Is<IReadOnlyList<Message>>(m =>
                m.Count == 1 && m[0].Role == MessageRole.Assistant && m[0].Content == "the answer"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Existing_conversation_feeds_history_to_the_loop_and_appends_both_turns()
    {
        var conversationId = Guid.NewGuid();
        var existing = Conversation.Create(conversationId, Owner, "Earlier");
        existing.AddMessage(Guid.NewGuid(), MessageRole.User, "previous");
        _store.GetAsync(conversationId, Owner, Arg.Any<CancellationToken>()).Returns(existing);
        ChatService service = CreateService();

        await SendAsync(service, Request(conversationId, "follow up"), TestContext.Current.CancellationToken);

        _orchestrator.Received(1).RunStreamingAsync(
            Arg.Is<AIOrchestrationRequest>(r =>
                r.Messages.Count == 2
                && r.Messages[0].Role == ChatRole.User && r.Messages[0].Text == "previous"
                && r.Messages[1].Text == "follow up"),
            Arg.Any<CancellationToken>());
        await _store.Received(1).AppendMessagesAsync(conversationId, Owner,
            Arg.Is<IReadOnlyList<Message>>(m => m.Count == 1 && m[0].Role == MessageRole.User && m[0].Content == "follow up"),
            Arg.Any<CancellationToken>());
        await _store.Received(1).AppendMessagesAsync(conversationId, Owner,
            Arg.Is<IReadOnlyList<Message>>(m => m.Count == 1 && m[0].Role == MessageRole.Assistant && m[0].Content == "the answer"),
            Arg.Any<CancellationToken>());
        await _store.DidNotReceive().CreateAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolved_mention_context_is_injected_before_the_user_message()
    {
        ChatService service = CreateService();
        _mentionContextResolver.ResolveContextAsync(Arg.Any<IReadOnlyList<AIMention>>(), Arg.Any<CancellationToken>())
            .Returns("<untrusted_document>invoice 42</untrusted_document>");

        await SendAsync(service,
            Request(message: "summarise", mentions: [new AIMention("invoice", "42")]),
            TestContext.Current.CancellationToken);

        _orchestrator.Received(1).RunStreamingAsync(
            Arg.Is<AIOrchestrationRequest>(r =>
                r.Messages.Count == 2
                && r.Messages[0].Role == ChatRole.User && r.Messages[0].Text!.Contains("invoice 42")
                && r.Messages[1].Role == ChatRole.User && r.Messages[1].Text == "summarise"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Mention_context_is_not_persisted_only_the_user_message_is()
    {
        ChatService service = CreateService();
        _mentionContextResolver.ResolveContextAsync(Arg.Any<IReadOnlyList<AIMention>>(), Arg.Any<CancellationToken>())
            .Returns("<untrusted_document>secret</untrusted_document>");

        await SendAsync(service,
            Request(message: "summarise", mentions: [new AIMention("invoice", "42")]),
            TestContext.Current.CancellationToken);

        await _store.Received(1).CreateAsync(
            Arg.Is<Conversation>(c =>
                c.Messages.Count == 1
                && c.Messages[0].Content == "summarise"
                && !c.Messages[0].Content.Contains("secret")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolved_attachment_context_is_injected_before_the_user_message()
    {
        ChatService service = CreateService();
        _attachmentTextResolver.ResolveContextAsync(Arg.Any<IReadOnlyList<AIAttachment>>(), Arg.Any<CancellationToken>())
            .Returns("<untrusted_document>report.pdf</untrusted_document>");

        await SendAsync(service,
            Request(message: "summarise", attachments: [new AIAttachment("blob-1", "report.pdf", "application/pdf", 64)]),
            TestContext.Current.CancellationToken);

        _orchestrator.Received(1).RunStreamingAsync(
            Arg.Is<AIOrchestrationRequest>(r =>
                r.Messages.Count == 2
                && r.Messages[0].Role == ChatRole.User && r.Messages[0].Text!.Contains("report.pdf")
                && r.Messages[1].Text == "summarise"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Attachment_context_is_not_persisted()
    {
        ChatService service = CreateService();
        _attachmentTextResolver.ResolveContextAsync(Arg.Any<IReadOnlyList<AIAttachment>>(), Arg.Any<CancellationToken>())
            .Returns("<untrusted_document>secret</untrusted_document>");

        await SendAsync(service,
            Request(message: "summarise", attachments: [new AIAttachment("blob-1", "report.pdf", "application/pdf", 64)]),
            TestContext.Current.CancellationToken);

        await _store.Received(1).CreateAsync(
            Arg.Is<Conversation>(c => c.Messages.Count == 1 && !c.Messages[0].Content.Contains("secret")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task No_mentions_inject_no_extra_message()
    {
        ChatService service = CreateService();

        await SendAsync(service, Request(message: "plain"), TestContext.Current.CancellationToken);

        _orchestrator.Received(1).RunStreamingAsync(
            Arg.Is<AIOrchestrationRequest>(r => r.Messages.Count == 1 && r.Messages[0].Text == "plain"),
            Arg.Any<CancellationToken>());
        await _mentionContextResolver.DidNotReceive()
            .ResolveContextAsync(Arg.Any<IReadOnlyList<AIMention>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_chat_capable_workspace_is_rejected_before_the_loop()
    {
        ChatService service = CreateService();
        _capabilityResolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AIModelCapabilities { Chat = false });

        // The guard runs in PrepareAsync, before the SSE stream opens, so it surfaces as an exception.
        await Should.ThrowAsync<WorkspaceNotChatCapableException>(
            () => service.PrepareAsync(Request(), TestContext.Current.CancellationToken));

        _orchestrator.DidNotReceive().RunStreamingAsync(Arg.Any<AIOrchestrationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unknown_conversation_for_the_owner_throws()
    {
        var conversationId = Guid.NewGuid();
        _store.GetAsync(conversationId, Owner, Arg.Any<CancellationToken>()).Returns((Conversation?)null);
        ChatService service = CreateService();

        await Should.ThrowAsync<ConversationNotFoundException>(
            () => service.PrepareAsync(Request(conversationId), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Clarification_interrupt_is_surfaced_and_the_question_is_persisted()
    {
        ChatService service = CreateService();
        var clarification = new AIClarificationRequest
        {
            Question = "Which environment?",
            Options = [new AIClarificationOption { Label = "Prod" }, new AIClarificationOption { Label = "Test" }],
            AllowOther = true,
        };
        string payload = JsonSerializer.Serialize(clarification, JsonSerializerOptions.Web);
        StubLoop(new AIOrchestrationResult
        {
            Content = string.Empty,
            Messages = [],
            Iterations = 1,
            MaxIterationsReached = false,
            ToolInvocations = [],
            Interrupt = new AIToolInterrupt(AIClarificationRequest.InterruptKind, payload),
        });

        ChatSendResult result = await SendAsync(service, Request(message: "deploy"), TestContext.Current.CancellationToken);

        result.Clarification.ShouldNotBeNull();
        result.Clarification.Question.ShouldBe("Which environment?");
        result.Clarification.Options.Count.ShouldBe(2);
        result.Clarification.AllowOther.ShouldBeTrue();
        // The question text becomes the assistant content and is persisted in history.
        result.Content.ShouldBe("Which environment?");
        await _store.Received(1).AppendMessagesAsync(
            Arg.Any<Guid>(), Owner,
            Arg.Is<IReadOnlyList<Message>>(m =>
                m.Count == 1 && m[0].Role == MessageRole.Assistant && m[0].Content == "Which environment?"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Suggested_actions_are_gathered_and_returned()
    {
        ChatService service = CreateService();
        _suggestionResolver.ResolveAsync(Arg.Any<AISuggestionContext>(), Arg.Any<CancellationToken>())
            .Returns([new AISuggestedAction { Type = "calendar.connect", Label = "Connect", DeepLink = "/settings/calendar" }]);

        ChatSendResult result = await SendAsync(service, Request(), TestContext.Current.CancellationToken);

        AISuggestedAction action = result.SuggestedActions.ShouldHaveSingleItem();
        action.Type.ShouldBe("calendar.connect");
        action.DeepLink.ShouldBe("/settings/calendar");
    }

    [Fact]
    public async Task User_default_workspace_setting_is_used_when_no_explicit_workspace()
    {
        ChatService service = CreateService();
        _settingProvider.GetOrNullAsync(AIChatSettingNames.DefaultWorkspace, Arg.Any<CancellationToken>())
            .Returns("my-workspace");

        await SendAsync(service, Request(), TestContext.Current.CancellationToken);

        _orchestrator.Received(1).RunStreamingAsync(
            Arg.Is<AIOrchestrationRequest>(r => r.WorkspaceName == "my-workspace"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reserved_auto_default_workspace_falls_back_to_the_configured_default()
    {
        ChatService service = CreateService();
        _settingProvider.GetOrNullAsync(AIChatSettingNames.DefaultWorkspace, Arg.Any<CancellationToken>())
            .Returns(AIChatSettingNames.ReservedAutoWorkspace);

        await SendAsync(service, Request(), TestContext.Current.CancellationToken);

        _orchestrator.Received(1).RunStreamingAsync(
            Arg.Is<AIOrchestrationRequest>(r => r.WorkspaceName == "default"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Explicit_request_workspace_overrides_the_user_default_setting()
    {
        ChatService service = CreateService();
        _settingProvider.GetOrNullAsync(AIChatSettingNames.DefaultWorkspace, Arg.Any<CancellationToken>())
            .Returns("user-default");

        await SendAsync(service, Request() with { WorkspaceName = "explicit" }, TestContext.Current.CancellationToken);

        _orchestrator.Received(1).RunStreamingAsync(
            Arg.Is<AIOrchestrationRequest>(r => r.WorkspaceName == "explicit"), Arg.Any<CancellationToken>());
        await _settingProvider.DidNotReceive()
            .GetOrNullAsync(AIChatSettingNames.DefaultWorkspace, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Prompt_badges_drive_the_composed_instruction_and_stamp_the_primary_prompt()
    {
        ChatService service = CreateService();
        var promptId = Guid.NewGuid();
        _promptBadgeResolver
            .ResolveAsync(Arg.Any<IReadOnlyList<Guid>>(), Owner, "my notes", Arg.Any<CancellationToken>())
            .Returns(new PromptBadgeResolution
            {
                ComposedMessage = "Summarise the content.\n\nmy notes",
                PrimaryPromptName = "Prompt:Summarize:Name",
                PrimaryPromptVersion = 3,
            });

        await SendAsync(service, Request(message: "my notes", promptRefs: [promptId]), TestContext.Current.CancellationToken);

        _orchestrator.Received(1).RunStreamingAsync(
            Arg.Is<AIOrchestrationRequest>(r =>
                r.Messages[r.Messages.Count - 1].Text == "Summarise the content.\n\nmy notes"
                && r.InvokedPromptName == "Prompt:Summarize:Name"
                && r.InvokedPromptVersion == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Prompt_badge_expansion_is_not_persisted_only_the_original_message_is()
    {
        ChatService service = CreateService();
        _promptBadgeResolver
            .ResolveAsync(Arg.Any<IReadOnlyList<Guid>>(), Owner, "my notes", Arg.Any<CancellationToken>())
            .Returns(new PromptBadgeResolution { ComposedMessage = "Summarise the content.\n\nmy notes", PrimaryPromptName = "X", PrimaryPromptVersion = 1 });

        await SendAsync(service, Request(message: "my notes", promptRefs: [Guid.NewGuid()]), TestContext.Current.CancellationToken);

        await _store.Received(1).CreateAsync(
            Arg.Is<Conversation>(c => c.Messages[0].Content == "my notes" && !c.Messages[0].Content.Contains("Summarise")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task No_prompt_refs_skips_badge_resolution()
    {
        ChatService service = CreateService();

        await SendAsync(service, Request(message: "plain"), TestContext.Current.CancellationToken);

        await _promptBadgeResolver.DidNotReceive()
            .ResolveAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _orchestrator.Received(1).RunStreamingAsync(
            Arg.Is<AIOrchestrationRequest>(r => r.InvokedPromptName == null && r.InvokedPromptVersion == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task User_custom_context_is_passed_to_the_orchestrator()
    {
        ChatService service = CreateService();
        _settingProvider.GetOrNullAsync(AIChatSettingNames.CustomContext, Arg.Any<CancellationToken>())
            .Returns("Always answer in British English.");

        await SendAsync(service, Request(), TestContext.Current.CancellationToken);

        _orchestrator.Received(1).RunStreamingAsync(
            Arg.Is<AIOrchestrationRequest>(r => r.UserCustomContext == "Always answer in British English."),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task User_custom_context_is_capped_at_the_maximum_length()
    {
        ChatService service = CreateService();
        _settingProvider.GetOrNullAsync(AIChatSettingNames.CustomContext, Arg.Any<CancellationToken>())
            .Returns(new string('x', AIChatSettingNames.MaxCustomContextLength + 500));

        await SendAsync(service, Request(), TestContext.Current.CancellationToken);

        _orchestrator.Received(1).RunStreamingAsync(
            Arg.Is<AIOrchestrationRequest>(r =>
                r.UserCustomContext!.Length == AIChatSettingNames.MaxCustomContextLength),
            Arg.Any<CancellationToken>());
    }
}
