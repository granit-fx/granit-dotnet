using Granit.AI.Chat.Domain;

namespace Granit.AI.Chat;

/// <summary>
/// Owner-scoped persistence for <see cref="Conversation"/> aggregates. Every method is scoped to
/// an owner so a caller can only ever reach their own conversations (owner-only isolation, ADR-067);
/// tenant isolation is applied by the underlying DbContext.
/// </summary>
public interface IConversationStore
{
    /// <summary>Persists a new conversation and returns it.</summary>
    Task<Conversation> CreateAsync(Conversation conversation, CancellationToken cancellationToken = default);

    /// <summary>Returns the owner's conversation including its messages, or <see langword="null"/>.</summary>
    Task<Conversation?> GetAsync(Guid id, Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>Returns the owner's conversations (without messages), newest first.</summary>
    Task<IReadOnlyList<Conversation>> ListAsync(Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends messages to the owner's conversation; <see langword="false"/> if it is not theirs.
    /// </summary>
    Task<bool> AppendMessagesAsync(
        Guid id,
        Guid ownerId,
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken = default);

    /// <summary>Renames the owner's conversation; <see langword="false"/> if it is not theirs.</summary>
    Task<bool> RenameAsync(Guid id, Guid ownerId, string title, CancellationToken cancellationToken = default);

    /// <summary>Deletes the owner's conversation; <see langword="false"/> if it is not theirs.</summary>
    Task<bool> DeleteAsync(Guid id, Guid ownerId, CancellationToken cancellationToken = default);
}
