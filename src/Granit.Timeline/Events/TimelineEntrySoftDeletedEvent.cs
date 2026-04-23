using Granit.Events;

namespace Granit.Timeline.Events;

/// <summary>
/// Raised when a timeline entry is soft-deleted (GDPR right to erasure).
/// System log entries cannot be deleted (ISO 27001 audit trail).
/// </summary>
public sealed record TimelineEntrySoftDeletedEvent(
    Guid EntryId,
    string EntityType,
    string EntityId) : IDomainEvent;
