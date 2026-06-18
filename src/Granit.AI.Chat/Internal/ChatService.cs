using System.Runtime.CompilerServices;
using System.Text.Json;
using Granit.AI.Chat.Attachments;
using Granit.AI.Chat.Clarification;
using Granit.AI.Chat.Domain;
using Granit.AI.Chat.Exceptions;
using Granit.AI.Chat.Mentions;
using Granit.AI.Chat.Settings;
using Granit.AI.Chat.Suggestions;
using Granit.AI.Exceptions;
using Granit.AI.Options;
using Granit.AI.Tools;
using Granit.AI.Workspaces;
using Granit.Guids;
using Granit.Settings.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Granit.AI.Chat.Internal;

/// <summary>
/// Default <see cref="IChatService"/>. Resolves and guards the workspace (<see cref="PrepareAsync"/>),
/// then streams the agentic tool loop over the conversation history, persisting the user turn before
/// the loop and the assistant turn at the end (<see cref="StreamAsync"/>).
/// </summary>
internal sealed class ChatService(
    IConversationStore conversationStore,
    IAIToolOrchestrator orchestrator,
    IAIWorkspaceProvider workspaceProvider,
    IAIWorkspaceCapabilityResolver capabilityResolver,
    IAIMentionContextResolver mentionContextResolver,
    IAIAttachmentTextResolver attachmentTextResolver,
    IAISuggestionResolver suggestionResolver,
    IPromptBadgeResolver promptBadgeResolver,
    ISettingProvider settingProvider,
    IGuidGenerator guidGenerator,
    IOptions<GranitAIOptions> aiOptions) : IChatService
{
    private const int MaxTitleLength = 100;

    public async Task<ChatSendHandle> PrepareAsync(ChatSendRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Message);

        string workspaceName = await ResolveWorkspaceNameAsync(request.WorkspaceName, cancellationToken).ConfigureAwait(false);
        AIWorkspace workspace = await workspaceProvider.GetAsync(workspaceName, cancellationToken).ConfigureAwait(false)
            ?? throw new AIWorkspaceNotFoundException(workspaceName);

        // Reject a workspace the catalog reports as non-chat before any model work starts. An
        // unknown capability set (null) is allowed — the catalog simply has no entry for the model.
        AIModelCapabilities? capabilities = await capabilityResolver
            .ResolveAsync(workspace.Provider, workspace.Model, cancellationToken)
            .ConfigureAwait(false);
        if (capabilities is { Chat: false })
        {
            throw new WorkspaceNotChatCapableException(workspaceName);
        }

        bool isNew = request.ConversationId is null;
        Conversation conversation = isNew
            ? Conversation.Create(guidGenerator.Create(), request.OwnerId, DeriveTitle(request.Message))
            : await conversationStore.GetAsync(request.ConversationId!.Value, request.OwnerId, cancellationToken).ConfigureAwait(false)
                ?? throw new ConversationNotFoundException(request.ConversationId.Value);

        List<ChatMessage> loopMessages = [.. conversation.Messages.Select(ToChatMessage)];

        // Resolve attached files to extracted text under the caller's ACLs and inject it ahead of
        // the message as untrusted data. Per-turn only — never persisted.
        if (request.Attachments is { Count: > 0 } attachments)
        {
            string? attachmentContext = await attachmentTextResolver
                .ResolveContextAsync(attachments, cancellationToken).ConfigureAwait(false);
            if (attachmentContext is not null)
            {
                loopMessages.Add(new ChatMessage(ChatRole.User, attachmentContext));
            }
        }

        // Resolve any @ mentions to context under the caller's ACLs and inject it ahead of the
        // message as untrusted data. It feeds this turn only and is never persisted.
        if (request.Mentions is { Count: > 0 } mentions)
        {
            string? mentionContext = await mentionContextResolver
                .ResolveContextAsync(mentions, cancellationToken).ConfigureAwait(false);
            if (mentionContext is not null)
            {
                loopMessages.Add(new ChatMessage(ChatRole.User, mentionContext));
            }
        }

        // Expand any '/' prompt badges (owner-scoped) and compose them with the free text into the
        // final instruction. The composed text drives this turn only; the user's original message is
        // what gets persisted, so the badge expansion stays ephemeral like mentions and attachments.
        PromptBadgeResolution badges = request.PromptRefs is { Count: > 0 } promptRefs
            ? await promptBadgeResolver.ResolveAsync(promptRefs, request.OwnerId, request.Message, cancellationToken).ConfigureAwait(false)
            : new PromptBadgeResolution { ComposedMessage = request.Message };

        loopMessages.Add(new ChatMessage(ChatRole.User, badges.ComposedMessage));

        return new ChatSendHandle
        {
            ConversationId = conversation.Id,
            Conversation = conversation,
            IsNew = isNew,
            OwnerId = request.OwnerId,
            OriginalMessage = request.Message,
            WorkspaceName = workspaceName,
            LoopMessages = loopMessages,
            InvokedPromptName = badges.PrimaryPromptName,
            InvokedPromptVersion = badges.PrimaryPromptVersion,
        };
    }

    public async IAsyncEnumerable<ChatTurnUpdate> StreamAsync(
        ChatSendHandle handle,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);

        // Persist the user turn before the loop runs: the message and conversation then survive a
        // crash mid-stream (only the regenerable assistant turn can be lost — see ADR-068).
        await PersistUserTurnAsync(handle, cancellationToken).ConfigureAwait(false);

        var orchestrationRequest = new AIOrchestrationRequest
        {
            WorkspaceName = handle.WorkspaceName,
            Messages = handle.LoopMessages,
            UserCustomContext = await ResolveCustomContextAsync(cancellationToken).ConfigureAwait(false),
            InvokedPromptName = handle.InvokedPromptName,
            InvokedPromptVersion = handle.InvokedPromptVersion,
        };

        AIOrchestrationResult? orchestrationResult = null;
        bool anyDelta = false;

        await foreach (AIOrchestrationUpdate update in orchestrator
            .RunStreamingAsync(orchestrationRequest, cancellationToken)
            .ConfigureAwait(false))
        {
            switch (update.Kind)
            {
                case AIOrchestrationUpdateKind.Delta:
                    anyDelta = true;
                    yield return ChatTurnUpdate.ForDelta(update.TextDelta!);
                    break;
                case AIOrchestrationUpdateKind.ToolCall:
                    yield return ChatTurnUpdate.ForToolCall(update.ToolName!, update.ToolCallId!);
                    break;
                case AIOrchestrationUpdateKind.ToolResult:
                    yield return ChatTurnUpdate.ForToolResult(update.ToolName!, update.ToolCallId!, update.Succeeded ?? true);
                    break;
                case AIOrchestrationUpdateKind.Completed:
                    orchestrationResult = update.Result;
                    break;
            }
        }

        AIOrchestrationResult result = orchestrationResult!;

        // A clarification halts the loop: the assistant turn records the question and the structured
        // request is surfaced so the front can render clickable options. The user's choice arrives as
        // the next turn and resumes the loop normally.
        AIClarificationRequest? clarification = TryReadClarification(result.Interrupt);
        string assistantContent = clarification?.Question ?? result.Content;

        // Never leave the text channel empty: if nothing streamed (a non-streaming provider, or a
        // clarification with no model text), emit the settled answer as a single delta — but not for a
        // clarification, whose content travels on the dedicated clarification frame.
        if (!anyDelta && clarification is null && assistantContent.Length > 0)
        {
            yield return ChatTurnUpdate.ForDelta(assistantContent);
        }

        await PersistAssistantTurnAsync(handle, assistantContent, cancellationToken).ConfigureAwait(false);

        // Gather typed, non-executing suggested actions from registered providers under the caller's
        // ACLs. Ephemeral — surfaced with the answer, never persisted.
        IReadOnlyList<AISuggestedAction> suggestedActions = await suggestionResolver
            .ResolveAsync(new AISuggestionContext(handle.OwnerId, handle.OriginalMessage), cancellationToken)
            .ConfigureAwait(false);

        yield return ChatTurnUpdate.ForCompleted(new ChatSendResult
        {
            ConversationId = handle.ConversationId,
            Content = assistantContent,
            MaxIterationsReached = result.MaxIterationsReached,
            InputTokens = result.InputTokens,
            OutputTokens = result.OutputTokens,
            SuggestedActions = suggestedActions,
            Clarification = clarification,
        });
    }

    private async Task PersistUserTurnAsync(ChatSendHandle handle, CancellationToken cancellationToken)
    {
        if (handle.IsNew)
        {
            handle.Conversation.AddMessage(guidGenerator.Create(), MessageRole.User, handle.OriginalMessage);
            await conversationStore.CreateAsync(handle.Conversation, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await conversationStore.AppendMessagesAsync(
                handle.ConversationId,
                handle.OwnerId,
                [Message.Create(guidGenerator.Create(), handle.ConversationId, MessageRole.User, handle.OriginalMessage)],
                cancellationToken).ConfigureAwait(false);
        }
    }

    private Task<bool> PersistAssistantTurnAsync(ChatSendHandle handle, string assistantContent, CancellationToken cancellationToken) =>
        conversationStore.AppendMessagesAsync(
            handle.ConversationId,
            handle.OwnerId,
            [Message.Create(guidGenerator.Create(), handle.ConversationId, MessageRole.Assistant, assistantContent)],
            cancellationToken);

    /// <summary>
    /// Parses a clarification from a loop interrupt, or <see langword="null"/> when the interrupt is
    /// absent, of another kind, or malformed (a malformed payload must not fail the turn).
    /// </summary>
    private static AIClarificationRequest? TryReadClarification(AIToolInterrupt? interrupt)
    {
        if (interrupt is null || interrupt.Kind != AIClarificationRequest.InterruptKind)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AIClarificationRequest>(interrupt.Payload, JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Resolves the workspace: an explicit per-request name wins; otherwise the user's default
    /// (unless it is the reserved <c>Auto</c>); otherwise the configured default workspace.
    /// </summary>
    private async Task<string> ResolveWorkspaceNameAsync(string? explicitWorkspace, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(explicitWorkspace))
        {
            return explicitWorkspace;
        }

        string? userDefault = await settingProvider
            .GetOrNullAsync(AIChatSettingNames.DefaultWorkspace, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(userDefault)
            && !string.Equals(userDefault, AIChatSettingNames.ReservedAutoWorkspace, StringComparison.OrdinalIgnoreCase))
        {
            return userDefault;
        }

        return aiOptions.Value.DefaultWorkspace;
    }

    /// <summary>
    /// Reads the user's free-text custom context, capped at the configured maximum, for the
    /// system-prompt composer. Returns <see langword="null"/> when unset or blank.
    /// </summary>
    private async Task<string?> ResolveCustomContextAsync(CancellationToken cancellationToken)
    {
        string? customContext = await settingProvider
            .GetOrNullAsync(AIChatSettingNames.CustomContext, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(customContext))
        {
            return null;
        }

        return customContext.Length > AIChatSettingNames.MaxCustomContextLength
            ? customContext[..AIChatSettingNames.MaxCustomContextLength]
            : customContext;
    }

    private static ChatMessage ToChatMessage(Message message) =>
        new(MapRole(message.Role), message.Content);

    private static ChatRole MapRole(MessageRole role) => role switch
    {
        MessageRole.User => ChatRole.User,
        MessageRole.Assistant => ChatRole.Assistant,
        MessageRole.System => ChatRole.System,
        MessageRole.Tool => ChatRole.Tool,
        _ => ChatRole.User,
    };

    private static string DeriveTitle(string message)
    {
        string firstLine = message.Trim().Split('\n', 2)[0].Trim();
        return firstLine.Length <= MaxTitleLength ? firstLine : firstLine[..MaxTitleLength];
    }
}
