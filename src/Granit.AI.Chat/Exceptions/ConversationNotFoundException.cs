namespace Granit.AI.Chat.Exceptions;

/// <summary>
/// Raised when a message targets a conversation that does not exist for the current owner
/// (missing, or owned by someone else — the two are indistinguishable by design).
/// </summary>
public sealed class ConversationNotFoundException(Guid conversationId)
    : InvalidOperationException($"Conversation '{conversationId}' was not found for the current user.")
{
    /// <summary>The conversation id that could not be resolved.</summary>
    public Guid ConversationId { get; } = conversationId;
}
