using Granit.AI.Chat.Attachments;
using Granit.AI.Chat.Domain;
using Granit.AI.Chat.Exceptions;
using Granit.AI.Chat.Mentions;
using Granit.AI.Exceptions;
using Granit.AI.Options;
using Granit.AI.Tools;
using Granit.AI.Workspaces;
using Granit.Guids;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Granit.AI.Chat.Internal;

/// <summary>
/// Default <see cref="IChatService"/>. Resolves and guards the workspace, runs the agentic tool
/// loop over the conversation history, and persists the user and assistant turns.
/// </summary>
internal sealed class ChatService(
    IConversationStore conversationStore,
    IAIToolOrchestrator orchestrator,
    IAIWorkspaceProvider workspaceProvider,
    IAIWorkspaceCapabilityResolver capabilityResolver,
    IAIMentionContextResolver mentionContextResolver,
    IAIAttachmentTextResolver attachmentTextResolver,
    IGuidGenerator guidGenerator,
    IOptions<GranitAIOptions> aiOptions) : IChatService
{
    private const int MaxTitleLength = 100;

    public async Task<ChatSendResult> SendAsync(ChatSendRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Message);

        string workspaceName = request.WorkspaceName ?? aiOptions.Value.DefaultWorkspace;
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

        loopMessages.Add(new ChatMessage(ChatRole.User, request.Message));

        AIOrchestrationResult result = await orchestrator.RunAsync(
            new AIOrchestrationRequest
            {
                WorkspaceName = workspaceName,
                Messages = loopMessages,
            },
            cancellationToken).ConfigureAwait(false);

        if (isNew)
        {
            conversation.AddMessage(guidGenerator.Create(), MessageRole.User, request.Message);
            conversation.AddMessage(guidGenerator.Create(), MessageRole.Assistant, result.Content);
            await conversationStore.CreateAsync(conversation, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await conversationStore.AppendMessagesAsync(
                conversation.Id,
                request.OwnerId,
                [
                    Message.Create(guidGenerator.Create(), conversation.Id, MessageRole.User, request.Message),
                    Message.Create(guidGenerator.Create(), conversation.Id, MessageRole.Assistant, result.Content),
                ],
                cancellationToken).ConfigureAwait(false);
        }

        return new ChatSendResult
        {
            ConversationId = conversation.Id,
            Content = result.Content,
            MaxIterationsReached = result.MaxIterationsReached,
            InputTokens = result.InputTokens,
            OutputTokens = result.OutputTokens,
        };
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
