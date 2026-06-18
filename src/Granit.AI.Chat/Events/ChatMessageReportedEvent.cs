using Granit.AI.Chat.Domain;
using Granit.Events;

namespace Granit.AI.Chat.Events;

/// <summary>
/// Raised when a user flags a chat <see cref="Message"/> for review. In-process domain event so an
/// application can route the signal (moderation queue, notification, metric) without coupling to the
/// chat module. Privacy (ADR-071): carries only identifiers and the user-entered reason — never the
/// reported message's content nor any tool arguments or results.
/// </summary>
/// <param name="ReportId">The persisted <see cref="MessageReport"/> identifier.</param>
/// <param name="MessageId">The flagged message.</param>
/// <param name="ConversationId">The conversation the message belongs to.</param>
/// <param name="OwnerId">The user who raised the report (the conversation owner).</param>
/// <param name="Reason">The free-text reason the user supplied.</param>
/// <param name="Category">The optional category the user selected.</param>
public sealed record ChatMessageReportedEvent(
    Guid ReportId,
    Guid MessageId,
    Guid ConversationId,
    Guid OwnerId,
    string Reason,
    MessageReportCategory? Category) : IDomainEvent;
