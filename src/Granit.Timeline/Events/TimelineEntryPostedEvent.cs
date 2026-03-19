using Granit.Core.Events;
using Granit.Timeline.Domain;

namespace Granit.Timeline.Events;

/// <summary>
/// Raised when a new timeline entry (comment, internal note, or system log) is created.
/// </summary>
public sealed record TimelineEntryPostedEvent(
    Guid EntryId,
    string EntityType,
    string EntityId,
    TimelineEntryType EntryType,
    string AuthorId) : IDomainEvent;
