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
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt)
{
    /// <summary>Projects a <see cref="Conversation"/> to a summary.</summary>
    public static ConversationSummaryResponse FromAggregate(Conversation conversation) =>
        new(conversation.Id, conversation.Title, conversation.IsFavorite, conversation.CreatedAt, conversation.ModifiedAt);
}

/// <summary>A message in a conversation.</summary>
/// <param name="Id">The message identifier.</param>
/// <param name="Role">
/// The author, lower-cased ("user"/"assistant"/"system"/"tool") to match the AI-SDK wire
/// convention the <c>@granit/ai-chat</c> client contract expects. The default
/// <see cref="System.Text.Json.Serialization.JsonStringEnumConverter"/> would emit PascalCase, so
/// the role is mapped explicitly here — see <see cref="ConversationResponse.FromAggregate"/>.
/// </param>
/// <param name="Content">The message text.</param>
/// <param name="CreatedAt">When the message was created.</param>
public sealed record MessageResponse(Guid Id, string Role, string Content, DateTimeOffset CreatedAt);

/// <summary>A conversation with its messages.</summary>
public sealed record ConversationResponse(
    Guid Id,
    string Title,
    Guid OwnerId,
    bool IsFavorite,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt,
    IReadOnlyList<MessageResponse> Messages)
{
    /// <summary>Projects a <see cref="Conversation"/> (with messages) to a response.</summary>
    public static ConversationResponse FromAggregate(Conversation conversation) =>
        new(
            conversation.Id,
            conversation.Title,
            conversation.OwnerId,
            conversation.IsFavorite,
            conversation.CreatedAt,
            conversation.ModifiedAt,
            [.. conversation.Messages.Select(m =>
                new MessageResponse(m.Id, m.Role.ToString().ToLowerInvariant(), m.Content, m.CreatedAt))]);
}
