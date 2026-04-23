using Granit.Timeline.Domain;

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

    /// <summary>Adds an attachment reference to an existing timeline entry.</summary>
    Task<TimelineAttachment> AddAttachmentAsync(
        Guid entryId,
        Guid blobId,
        string fileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default);
}
