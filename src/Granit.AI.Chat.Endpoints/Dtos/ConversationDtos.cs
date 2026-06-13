using Granit.AI.Chat.Domain;

namespace Granit.AI.Chat.Endpoints.Dtos;

/// <summary>Request to create a conversation.</summary>
/// <param name="Title">Display title for the new conversation.</param>
public sealed record CreateConversationRequest(string Title);

/// <summary>Request to rename a conversation.</summary>
/// <param name="Title">The new title.</param>
public sealed record RenameConversationRequest(string Title);

/// <summary>A conversation in a list (without messages).</summary>
public sealed record ConversationSummaryResponse(
    Guid Id,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt)
{
    /// <summary>Projects a <see cref="Conversation"/> to a summary.</summary>
    public static ConversationSummaryResponse FromAggregate(Conversation conversation) =>
        new(conversation.Id, conversation.Title, conversation.CreatedAt, conversation.ModifiedAt);
}

/// <summary>A message in a conversation.</summary>
public sealed record MessageResponse(Guid Id, string Role, string Content, DateTimeOffset CreatedAt);

/// <summary>A conversation with its messages.</summary>
public sealed record ConversationResponse(
    Guid Id,
    string Title,
    Guid OwnerId,
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
            conversation.CreatedAt,
            conversation.ModifiedAt,
            [.. conversation.Messages.Select(m =>
                new MessageResponse(m.Id, m.Role.ToString(), m.Content, m.CreatedAt))]);
}
