namespace Granit.AI.Endpoints.Dtos;

/// <summary>
/// Request for an AI chat completion.
/// </summary>
/// <param name="Messages">Ordered list of chat messages.</param>
public sealed record AIChatRequest(
    IReadOnlyList<AIChatMessageRequest> Messages);

/// <summary>
/// A single message in a chat conversation.
/// </summary>
/// <param name="Role">Message role: <c>user</c>, <c>assistant</c>, or <c>system</c>.</param>
/// <param name="Content">Message content.</param>
public sealed record AIChatMessageRequest(
    string Role,
    string Content);
