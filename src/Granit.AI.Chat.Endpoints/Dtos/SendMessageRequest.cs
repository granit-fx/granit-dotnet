namespace Granit.AI.Chat.Endpoints.Dtos;

/// <summary>Request to send a message and receive a streamed answer.</summary>
/// <param name="Message">The user's message.</param>
/// <param name="ConversationId">The conversation to continue, or <see langword="null"/> to start a new one.</param>
/// <param name="WorkspaceName">The chat-capable workspace to use, or <see langword="null"/> for the default.</param>
public sealed record SendMessageRequest(
    string Message,
    Guid? ConversationId = null,
    string? WorkspaceName = null);

/// <summary>
/// A frame streamed over SSE for a send. <see cref="Type"/> discriminates the frame:
/// <c>conversation</c> (carries <see cref="ConversationId"/>), <c>delta</c> (carries a
/// <see cref="Content"/> chunk), or <c>usage</c> (carries the token counts).
/// </summary>
public sealed record ChatStreamEvent(
    string Type,
    string? Content = null,
    Guid? ConversationId = null,
    int? InputTokens = null,
    int? OutputTokens = null);
