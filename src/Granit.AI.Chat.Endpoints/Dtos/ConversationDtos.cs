using Granit.AI.Chat.Domain;

namespace Granit.AI.Chat.Endpoints.Dtos;

/// <summary>Request to create a conversation.</summary>
/// <param name="Title">Display title for the new conversation.</param>
public sealed record CreateConversationRequest(string Title);

/// <summary>Request to rename a conversation.</summary>
/// <param name="Title">The new title.</param>
public sealed record RenameConversationRequest(string Title);

/// <summary>Request to set a conversation's favorite flag to an explicit state (not a toggle).</summary>
/// <param name="IsFavorite">The desired favorite state.</param>
public sealed record SetConversationFavoriteRequest(bool IsFavorite);

/// <summary>A conversation in a list (without messages).</summary>
public sealed record ConversationSummaryResponse(
    Guid Id,
    string Title,
    bool IsFavorite,
    string? WorkspaceKey,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt)
{
    /// <summary>Projects a <see cref="Conversation"/> to a summary.</summary>
    public static ConversationSummaryResponse FromAggregate(Conversation conversation) =>
        new(conversation.Id, conversation.Title, conversation.IsFavorite, conversation.WorkspaceKey, conversation.CreatedAt, conversation.ModifiedAt);
}

/// <summary>A message in a conversation.</summary>
/// <param name="Id">The message identifier.</param>
/// <param name="Role">
/// The author, lower-cased ("user"/"assistant"/"system"/"tool") to match the AI-SDK wire
/// convention the <c>@granit/ai-chat</c> client contract expects. The default
/// <see cref="System.Text.Json.Serialization.JsonStringEnumConverter"/> would emit PascalCase, so
/// the role is mapped explicitly here — see <see cref="MessageResponse.FromEntity"/>.
/// </param>
/// <param name="Content">The message text.</param>
/// <param name="WorkspaceKey">
/// Machine key of the workspace that produced this message, or <see langword="null"/> for messages
/// predating this field. Lets the UI show which AI was used per turn.
/// </param>
/// <param name="CreatedAt">When the message was created.</param>
public sealed record MessageResponse(Guid Id, string Role, string Content, string? WorkspaceKey, DateTimeOffset CreatedAt)
{
    /// <summary>Projects a <see cref="Message"/> to a response, lower-casing the role for the wire.</summary>
    public static MessageResponse FromEntity(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new MessageResponse(
            message.Id,
            message.Role.ToString().ToLowerInvariant(),
            message.Content,
            message.WorkspaceKey,
            message.CreatedAt);
    }
}

/// <summary>
/// A conversation's metadata (title, favorite, timestamps), without its messages. The thread is
/// paged separately via <c>GET /conversations/{id}/messages</c>, so detail reads never materialise
/// an entire history just to render the header.
/// </summary>
public sealed record ConversationResponse(
    Guid Id,
    string Title,
    Guid OwnerId,
    bool IsFavorite,
    string? WorkspaceKey,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt)
{
    /// <summary>Projects a <see cref="Conversation"/> to its metadata response.</summary>
    public static ConversationResponse FromAggregate(Conversation conversation) =>
        new(
            conversation.Id,
            conversation.Title,
            conversation.OwnerId,
            conversation.IsFavorite,
            conversation.WorkspaceKey,
            conversation.CreatedAt,
            conversation.ModifiedAt);
}
