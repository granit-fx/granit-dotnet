using Granit.AI.Chat.Events;
using Granit.Domain;

namespace Granit.AI.Chat.Domain;

/// <summary>
/// A user's report (feedback) flagging a chat <see cref="Message"/> for review. Append-only and
/// multi-tenant; owned by the reporter, who must own the message's <see cref="Conversation"/>
/// (owner-only isolation, ADR-067). Privacy (ADR-071): stores identifiers plus the user-entered
/// reason — never the reported message's content.
/// </summary>
public sealed class MessageReport : CreationAuditedAggregateRoot, IMultiTenant, IOwnable
{
    /// <summary>Maximum length of the free-text reason.</summary>
    public const int MaxReasonLength = 2000;

    private MessageReport()
    {
    }

    /// <summary>
    /// Creates a report flagging <paramref name="messageId"/> and raises
    /// <see cref="ChatMessageReportedEvent"/>. The tenant is stamped by the persistence interceptor.
    /// </summary>
    public static MessageReport Create(
        Guid id,
        Guid messageId,
        Guid conversationId,
        Guid ownerId,
        string reason,
        MessageReportCategory? category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var report = new MessageReport
        {
            Id = id,
            MessageId = messageId,
            ConversationId = conversationId,
            OwnerId = ownerId,
            Reason = reason,
            Category = category,
        };

        report.AddDomainEvent(
            new ChatMessageReportedEvent(id, messageId, conversationId, ownerId, reason, category));

        return report;
    }

    /// <summary>The flagged message.</summary>
    public Guid MessageId { get; private set; }

    /// <summary>The conversation the flagged message belongs to.</summary>
    public Guid ConversationId { get; private set; }

    /// <summary>The user who raised the report (the conversation owner).</summary>
    public Guid OwnerId { get; private set; }

    /// <summary>The free-text reason the user supplied.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>The optional category the user selected.</summary>
    public MessageReportCategory? Category { get; private set; }

    /// <summary>Owning tenant; stamped by the interceptor.</summary>
    public Guid? TenantId { get; private set; }

    /// <inheritdoc/>
    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }
}
