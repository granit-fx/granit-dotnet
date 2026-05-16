using Granit.Timeline.Abstractions;
using Granit.Timeline.Domain;
using Granit.Timeline.Exceptions;

namespace Granit.Timeline.Internal;

/// <summary>
/// Shared four-gate policy enforced by every <see cref="ITimelineWriter"/>
/// implementation before mutating <see cref="TimelineEntry.Body"/>. Centralised
/// so the in-memory and EF Core stores cannot drift, and so the front-end
/// gating rule has a single source of truth on the server.
/// </summary>
/// <remarks>
/// Order matters: external origin and SystemLog are immutable invariants; the
/// authorship check leaks no information about other users' content; the
/// window check is last so a UX timeout doesn't mask a permission problem.
/// </remarks>
internal static class TimelineEditGate
{
    public static void EnsureEditable(
        TimelineEntry entry,
        string? currentUserId,
        DateTimeOffset now,
        TimeSpan editWindow)
    {
        // Gate 1 — origin must be Native. Shadow rows anchoring external sources
        // are pointers to immutable upstream entries; their body is a snapshot.
        if (entry.SourceKey is not null)
        {
            throw new TimelineEntryNotEditableException(
                TimelineEntryNotEditableReason.ExternalOrigin,
                $"Timeline entry '{entry.Id}' is projected from source '{entry.SourceKey}' and cannot be edited.");
        }

        // Gate 2 — SystemLog entries are immutable (ISO 27001).
        if (entry.EntryType == TimelineEntryType.SystemLog)
        {
            throw new TimelineEntryNotEditableException(
                TimelineEntryNotEditableReason.SystemLog,
                $"Timeline entry '{entry.Id}' is a SystemLog and cannot be edited.");
        }

        // Gate 3 — caller must be the author. No staff-override path: a moderator
        // who needs to "correct" someone else's comment soft-deletes + reposts
        // under their own name (chain-of-custody clean).
        if (string.IsNullOrEmpty(currentUserId) ||
            !string.Equals(entry.AuthorId, currentUserId, StringComparison.Ordinal))
        {
            throw new TimelineEntryNotEditableException(
                TimelineEntryNotEditableReason.NotAuthor,
                $"Timeline entry '{entry.Id}' can only be edited by its author.");
        }

        // Gate 4 — edit window must not have elapsed (TimeSpan.Zero disables edits).
        if (editWindow <= TimeSpan.Zero || now - entry.CreatedAt > editWindow)
        {
            throw new TimelineEntryNotEditableException(
                TimelineEntryNotEditableReason.WindowExpired,
                $"Timeline entry '{entry.Id}' is past the {editWindow} edit window.");
        }
    }
}
