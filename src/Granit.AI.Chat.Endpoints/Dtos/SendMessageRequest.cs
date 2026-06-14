namespace Granit.AI.Chat.Endpoints.Dtos;

/// <summary>Request to send a message and receive a streamed answer.</summary>
/// <param name="Message">The user's message.</param>
/// <param name="ConversationId">The conversation to continue, or <see langword="null"/> to start a new one.</param>
/// <param name="WorkspaceName">The chat-capable workspace to use, or <see langword="null"/> for the default.</param>
/// <param name="Mentions">Entities <c>@</c>-referenced for this turn, resolved to context under the caller's ACLs.</param>
public sealed record SendMessageRequest(
    string Message,
    Guid? ConversationId = null,
    string? WorkspaceName = null,
    IReadOnlyList<MentionRequest>? Mentions = null);

/// <summary>An <c>@</c> mention on a send: a typed reference to an application entity.</summary>
/// <param name="Type">The mention type, matching an application-registered resolver.</param>
/// <param name="Id">The referenced entity's identifier, interpreted by the resolver for that type.</param>
public sealed record MentionRequest(string Type, string Id);

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
