using Granit.Domain;

namespace Granit.AI.Chat.Domain;

/// <summary>
/// A multi-turn chat conversation. Multi-tenant and <strong>private to its owner</strong> (v1,
/// ADR-067). Aggregate root over its <see cref="Messages"/>.
/// </summary>
public sealed class Conversation : FullAuditedAggregateRoot, IMultiTenant, IOwnable
{
    private Conversation()
    {
    }

    /// <summary>
    /// Creates a conversation owned by <paramref name="ownerId"/>. The tenant is stamped by the
    /// persistence interceptor on save.
    /// </summary>
    public static Conversation Create(
        Guid id,
        Guid ownerId,
        string title,
        string? workspaceKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        return new Conversation
        {
            Id = id,
            OwnerId = ownerId,
            Title = title,
            WorkspaceKey = workspaceKey,
        };
    }

    /// <summary>Display title.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>The user who owns this conversation.</summary>
    public Guid OwnerId { get; private set; }

    /// <summary>Whether the owner has marked this conversation as a favorite.</summary>
    public bool IsFavorite { get; private set; }

    /// <summary>
    /// Machine key of the workspace used when the conversation was created (e.g.
    /// <c>support-chat</c>), or <see langword="null"/> for conversations predating this field.
    /// Frozen at creation — subsequent turns do not overwrite it.
    /// </summary>
    public string? WorkspaceKey { get; private set; }

    /// <summary>The messages exchanged, oldest first.</summary>
    public List<Message> Messages { get; private set; } = [];

    /// <summary>Owning tenant; stamped by the interceptor.</summary>
    public Guid? TenantId { get; private set; }

    /// <inheritdoc/>
    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>Renames the conversation.</summary>
    public void Rename(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title;
    }

    /// <summary>Sets the favorite flag to the given state (idempotent — not a toggle).</summary>
    public void SetFavorite(bool isFavorite) => IsFavorite = isFavorite;

    /// <summary>Appends a message and returns it.</summary>
    public Message AddMessage(Guid id, MessageRole role, string content, string? workspaceKey = null)
    {
        var message = Message.Create(id, Id, role, content, workspaceKey);
        Messages.Add(message);
        return message;
    }
}
