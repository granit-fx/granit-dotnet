using Granit.Domain;

namespace Granit.AI.Chat.Domain;

/// <summary>
/// A single message in a <see cref="Conversation"/>. Append-only child entity of the conversation
/// aggregate; its tenant/owner scope is inherited from its conversation.
/// </summary>
public sealed class Message : CreationAuditedEntity
{
    private Message()
    {
    }

    /// <summary>Creates a message belonging to <paramref name="conversationId"/>.</summary>
    public static Message Create(
        Guid id,
        Guid conversationId,
        MessageRole role,
        string content,
        string? workspaceKey = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        return new Message
        {
            Id = id,
            ConversationId = conversationId,
            Role = role,
            Content = content,
            WorkspaceKey = workspaceKey,
        };
    }

    /// <summary>The owning conversation.</summary>
    public Guid ConversationId { get; private set; }

    /// <summary>Who authored the message.</summary>
    public MessageRole Role { get; private set; }

    /// <summary>The message text.</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>
    /// Machine key of the workspace that produced this message (e.g. <c>support-chat</c>),
    /// or <see langword="null"/> for messages predating this field. Allows the UI to show
    /// which AI was used per turn and to restore the workspace selector.
    /// </summary>
    public string? WorkspaceKey { get; private set; }
}
