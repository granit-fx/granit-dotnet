using Granit.AI.Chat.Attachments;
using Granit.AI.Chat.Clarification;
using Granit.AI.Chat.Domain;
using Granit.AI.Chat.Mentions;
using Granit.AI.Chat.Suggestions;
using Granit.AI.Exceptions;
using Microsoft.Extensions.AI;

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

    /// <summary>
    /// Catalogue prompt templates the user inserted as <c>/</c> badges for this turn, resolved under
    /// the caller's ACLs and composed with <see cref="Message"/> into the final instruction. The first
    /// resolved prompt is stamped into the usage record. <see langword="null"/> or empty when none.
    /// </summary>
    public IReadOnlyList<Guid>? PromptRefs { get; init; }
}

/// <summary>
/// A message persisted during a turn, projected so the streaming endpoint can surface the turn's real
/// identities (server-assigned id + timestamp) to the client without re-loading the conversation.
/// Mirrors the fields of the conversation read model's message projection (id, role, content,
/// created-at); <see cref="Role"/> is lower-cased ("user"/"assistant") to match the client wire convention.
/// </summary>
/// <param name="Id">The server-assigned message identifier.</param>
/// <param name="Role">The author, lower-cased ("user"/"assistant") to match the client wire convention.</param>
/// <param name="Content">The message text, as persisted.</param>
/// <param name="WorkspaceKey">
/// Machine key of the workspace that produced this message, or <see langword="null"/> for messages
/// predating this field. Mirrors <c>MessageResponse.WorkspaceKey</c>.
/// </param>
/// <param name="CreatedAt">The server creation timestamp.</param>
public sealed record PersistedChatMessage(Guid Id, string Role, string Content, string? WorkspaceKey, DateTimeOffset CreatedAt);

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

    /// <summary>
    /// Set when the agent asked a clarifying question instead of answering (the turn is blocked
    /// until the user picks an option). <see cref="Content"/> then carries the question text.
    /// </summary>
    public AIClarificationRequest? Clarification { get; init; }

    /// <summary>
    /// The turn's newly-persisted messages — the user message (saved before the loop) then the
    /// assistant message (saved after it), oldest-first — carrying their real ids and server
    /// timestamps. The streaming endpoint emits these as a <c>persisted</c> frame so the client can
    /// render the actual rows (and report the just-streamed message) instead of synthesising ids.
    /// Empty when no turn was persisted.
    /// </summary>
    public IReadOnlyList<PersistedChatMessage> PersistedMessages { get; init; } = [];
}

/// <summary>
/// A resolved, validated send ready to run: the (possibly new) conversation, the loop history with
/// per-turn context already injected, and the workspace. Produced by <see cref="IChatService.PrepareAsync"/>
/// and consumed by <see cref="IChatService.StreamAsync"/>. <see cref="ConversationId"/> is known before
/// the agent runs, so the endpoint can flush the SSE <c>conversation</c> frame immediately; the rest of
/// the resolved state is internal to the chat module.
/// </summary>
public sealed record ChatSendHandle
{
    /// <summary>The conversation the message will be added to (new or existing), known up front.</summary>
    public required Guid ConversationId { get; init; }

    /// <summary>The resolved conversation aggregate (new or loaded under the caller's ownership).</summary>
    internal Conversation Conversation { get; init; } = null!;

    /// <summary>Whether the conversation is new and must be created (vs. appended to).</summary>
    internal bool IsNew { get; init; }

    /// <summary>The owner the turn is persisted and resolved under.</summary>
    internal Guid OwnerId { get; init; }

    /// <summary>The user's original message, as persisted (before badge expansion).</summary>
    internal string OriginalMessage { get; init; } = string.Empty;

    /// <summary>The resolved chat-capable workspace to run the loop against.</summary>
    internal string WorkspaceName { get; init; } = string.Empty;

    /// <summary>The loop history with attachment, mention and prompt-badge context already injected.</summary>
    internal IReadOnlyList<ChatMessage> LoopMessages { get; init; } = [];

    /// <summary>The primary prompt-template name stamped into the usage record, if any.</summary>
    internal string? InvokedPromptName { get; init; }

    /// <summary>The primary prompt-template version stamped into the usage record, if any.</summary>
    internal int? InvokedPromptVersion { get; init; }
}

/// <summary>Discriminates a <see cref="ChatTurnUpdate"/>.</summary>
public enum ChatTurnUpdateKind
{
    /// <summary>An incremental slice of assistant text.</summary>
    Delta,

    /// <summary>A tool invocation started.</summary>
    ToolCall,

    /// <summary>A tool invocation finished.</summary>
    ToolResult,

    /// <summary>The terminal update carrying the settled <see cref="ChatSendResult"/>.</summary>
    Completed,
}

/// <summary>
/// One update streamed by <see cref="IChatService.StreamAsync"/> as a chat turn runs: assistant text
/// deltas and tool activity, ending with exactly one <see cref="ChatTurnUpdateKind.Completed"/> update
/// carrying the persisted <see cref="ChatSendResult"/> (usage, suggestions, clarification).
/// </summary>
public sealed record ChatTurnUpdate
{
    /// <summary>Which kind of update this is.</summary>
    public required ChatTurnUpdateKind Kind { get; init; }

    /// <summary>The text slice, set when <see cref="Kind"/> is <see cref="ChatTurnUpdateKind.Delta"/>.</summary>
    public string? Delta { get; init; }

    /// <summary>The tool's wire name, set for <see cref="ChatTurnUpdateKind.ToolCall"/> / <see cref="ChatTurnUpdateKind.ToolResult"/>.</summary>
    public string? ToolName { get; init; }

    /// <summary>The model-issued call id correlating a tool call to its result.</summary>
    public string? ToolCallId { get; init; }

    /// <summary>Whether the tool succeeded, set for <see cref="ChatTurnUpdateKind.ToolResult"/>.</summary>
    public bool? Succeeded { get; init; }

    /// <summary>The settled, persisted result, set when <see cref="Kind"/> is <see cref="ChatTurnUpdateKind.Completed"/>.</summary>
    public ChatSendResult? Result { get; init; }

    /// <summary>Creates a text-delta update.</summary>
    public static ChatTurnUpdate ForDelta(string delta) =>
        new() { Kind = ChatTurnUpdateKind.Delta, Delta = delta };

    /// <summary>Creates a tool-call-started update.</summary>
    public static ChatTurnUpdate ForToolCall(string toolName, string callId) =>
        new() { Kind = ChatTurnUpdateKind.ToolCall, ToolName = toolName, ToolCallId = callId };

    /// <summary>Creates a tool-call-finished update.</summary>
    public static ChatTurnUpdate ForToolResult(string toolName, string callId, bool succeeded) =>
        new() { Kind = ChatTurnUpdateKind.ToolResult, ToolName = toolName, ToolCallId = callId, Succeeded = succeeded };

    /// <summary>Creates the terminal completion update.</summary>
    public static ChatTurnUpdate ForCompleted(ChatSendResult result) =>
        new() { Kind = ChatTurnUpdateKind.Completed, Result = result };
}

/// <summary>
/// Drives a chat turn (ADR-067): guards workspace chat-capability, runs the agentic tool loop over
/// the conversation's history within the caller's ACLs, and persists the user and assistant messages.
/// </summary>
/// <remarks>
/// The turn is split into two phases so the SSE response can commit its headers in milliseconds:
/// <see cref="PrepareAsync"/> does all the fast validation and context resolution (and can still fail
/// with an HTTP-mappable exception <em>before</em> the stream opens), then <see cref="StreamAsync"/>
/// streams the agentic loop's text and tool activity and persists — by which point the endpoint has
/// already flushed the <c>conversation</c> frame.
/// </remarks>
public interface IChatService
{
    /// <summary>
    /// Validates and resolves the send — workspace + chat-capability, conversation ownership, and the
    /// per-turn mention/attachment/prompt context — into a <see cref="ChatSendHandle"/> whose
    /// <see cref="ChatSendHandle.ConversationId"/> is known before the agent runs. Fast: no model work.
    /// </summary>
    /// <exception cref="Exceptions.WorkspaceNotChatCapableException">The workspace is not chat-capable.</exception>
    /// <exception cref="AIWorkspaceNotFoundException">The resolved workspace does not exist.</exception>
    /// <exception cref="Exceptions.ConversationNotFoundException">The target conversation is not the caller's.</exception>
    Task<ChatSendHandle> PrepareAsync(ChatSendRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams the agentic loop for a prepared handle — assistant text deltas and tool activity as they
    /// happen — persisting the user turn before the loop and the assistant turn at the end. Ends with a
    /// single <see cref="ChatTurnUpdateKind.Completed"/> update.
    /// </summary>
    IAsyncEnumerable<ChatTurnUpdate> StreamAsync(ChatSendHandle handle, CancellationToken cancellationToken = default);
}
