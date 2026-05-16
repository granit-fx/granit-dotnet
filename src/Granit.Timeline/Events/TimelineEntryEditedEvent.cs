using Granit.Events;
using Granit.Timeline.Domain;

namespace Granit.Timeline.Events;

/// <summary>
/// Raised when a <see cref="TimelineEntry"/> body is edited by its author
/// within the configured edit window. Subscribers typically invalidate
/// AI summary caches, refresh notification snapshots, or forward the change
/// to the audit trail.
/// </summary>
/// <param name="EntryId">Identifier of the edited entry.</param>
/// <param name="EntityType">Type of the parent entity (timeline subject).</param>
/// <param name="EntityId">Identifier of the parent entity (timeline subject).</param>
/// <param name="AuthorId">Identifier of the author — always equal to the editing user (gated at writer).</param>
/// <param name="EditedAt">Timestamp of the edit.</param>
public sealed record TimelineEntryEditedEvent(
    Guid EntryId,
    string EntityType,
    string EntityId,
    string AuthorId,
    DateTimeOffset EditedAt) : IDomainEvent;
