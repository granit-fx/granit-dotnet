namespace Granit.AI.Endpoints.Dtos;

/// <summary>
/// A frame streamed over Server-Sent Events for a chat completion. <see cref="Type"/> discriminates
/// the frame: <c>delta</c> (an incremental <see cref="Content"/> chunk), <c>usage</c> (the token
/// counts), or <c>error</c> (a provider failure that occurred after streaming started). End-of-stream
/// is signalled by the SSE stream closing — there is no sentinel frame.
/// </summary>
/// <param name="Type">The frame discriminator: <c>delta</c>, <c>usage</c>, or <c>error</c>.</param>
/// <param name="Content">The incremental content chunk for a <c>delta</c> frame.</param>
/// <param name="InputTokens">Input token count for a <c>usage</c> frame.</param>
/// <param name="OutputTokens">Output token count for a <c>usage</c> frame.</param>
/// <param name="Error">The error message for an <c>error</c> frame.</param>
public sealed record AIChatStreamEvent(
    string Type,
    string? Content = null,
    int? InputTokens = null,
    int? OutputTokens = null,
    string? Error = null);
