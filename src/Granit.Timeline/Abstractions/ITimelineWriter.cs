using Granit.Timeline.Domain;
using Granit.Timeline.Exceptions;

namespace Granit.Timeline.Abstractions;

/// <summary>
/// Write operations for timeline entries and attachments.
/// </summary>
public interface ITimelineWriter
{
    /// <summary>Posts a new entry to the activity stream of an entity.</summary>
    Task<TimelineEntry> PostEntryAsync(
        string entityType,
        string entityId,
        TimelineEntryType entryType,
        string body,
        Guid? parentEntryId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a comment or internal note (GDPR right to erasure).
    /// Throws <see cref="InvalidOperationException"/> for <see cref="TimelineEntryType.SystemLog"/>
    /// entries because they are immutable (ISO 27001 audit trail).
    /// </summary>
    Task DeleteEntryAsync(Guid entryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the body of an entry authored by the current user, subject to
    /// the four gates (Native origin, non-SystemLog type, authorship, edit
    /// window). Throws <see cref="KeyNotFoundException"/> if the entry does
    /// not exist, and <see cref="TimelineEntryNotEditableException"/> when a
    /// gate rejects the request.
    /// </summary>
    Task UpdateEntryBodyAsync(
        Guid entryId,
        string newBody,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Materializes (or returns the existing) shadow row for an external entry
    /// projected by an <see cref="ITimelineSource"/>. The shadow row carries
    /// the source's snapshot (body, occurred-at, author) at anchor time and
    /// becomes the FK target for reactions and threaded replies. Idempotent:
    /// concurrent callers with the same <paramref name="sourceKey"/> +
    /// <paramref name="sourceId"/> all converge on the same row.
    /// </summary>
    /// <returns>The anchored entry's primary key (deterministic v5 GUID).</returns>
    /// <exception cref="KeyNotFoundException">No registered source matches <paramref name="sourceKey"/>, or the source has no entry for <paramref name="sourceId"/>.</exception>
    Task<Guid> AnchorExternalAsync(
        string entityType,
        string entityId,
        string sourceKey,
        string sourceId,
        CancellationToken cancellationToken = default);

    /// <summary>Adds an attachment reference to an existing timeline entry.</summary>
    Task<TimelineAttachment> AddAttachmentAsync(
        Guid entryId,
        Guid blobId,
        string fileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default);
}
