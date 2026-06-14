using Granit.AI.Chat.Attachments;
using Granit.AI.Chat.Mentions;
using Granit.AI.Chat.Suggestions;

namespace Granit.AI.Chat;

/// <summary>A request to send a message and obtain a tool-grounded answer.</summary>
public sealed record ChatSendRequest
{
    /// <summary>The existing conversation to continue, or <see langword="null"/> to start a new one.</summary>
    public Guid? ConversationId { get; init; }

    /// <summary>The user sending the message (owner of the conversation).</summary>
    public required Guid OwnerId { get; init; }

    /// <summary>The chat-capable workspace to use, or <see langword="null"/> for the default.</summary>
    public string? WorkspaceName { get; init; }

    /// <summary>The user's message.</summary>
    public required string Message { get; init; }

    /// <summary>
    /// Entities the user <c>@</c>-referenced for this turn, resolved to context under the caller's
    /// ACLs and injected ahead of the message. <see langword="null"/> or empty when none.
    /// </summary>
    public IReadOnlyList<AIMention>? Mentions { get; init; }

    /// <summary>
    /// Files the user attached for this turn, resolved to extracted text under the caller's ACLs
    /// and injected ahead of the message as untrusted context. <see langword="null"/> or empty when none.
    /// </summary>
    public IReadOnlyList<AIAttachment>? Attachments { get; init; }
}

/// <summary>The outcome of a send: the (possibly new) conversation and the assistant's answer.</summary>
public sealed record ChatSendResult
{
    /// <summary>The conversation the message was added to (new or existing).</summary>
    public required Guid ConversationId { get; init; }

    /// <summary>The assistant's final answer.</summary>
    public required string Content { get; init; }

    /// <summary>Whether the agent hit the iteration cap before settling.</summary>
    public bool MaxIterationsReached { get; init; }

    /// <summary>Total input tokens, if reported.</summary>
    public int? InputTokens { get; init; }

    /// <summary>Total output tokens, if reported.</summary>
    public int? OutputTokens { get; init; }

    /// <summary>
    /// Typed, non-executing suggested actions surfaced alongside the answer (deep links the front
    /// renders). Empty when no provider offered any. See <see cref="AISuggestedAction"/>.
    /// </summary>
    public IReadOnlyList<AISuggestedAction> SuggestedActions { get; init; } = [];
}

/// <summary>
/// Drives a chat turn (ADR-067): guards workspace chat-capability, runs the agentic tool loop over
/// the conversation's history within the caller's ACLs, and persists the user and assistant messages.
/// </summary>
public interface IChatService
{
    /// <summary>Sends a message and returns the persisted, tool-grounded answer.</summary>
    /// <exception cref="Exceptions.WorkspaceNotChatCapableException">The workspace is not chat-capable.</exception>
    /// <exception cref="Exceptions.ConversationNotFoundException">The target conversation is not the caller's.</exception>
    Task<ChatSendResult> SendAsync(ChatSendRequest request, CancellationToken cancellationToken = default);
}
