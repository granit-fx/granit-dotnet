using Granit.Timeline;

namespace Granit.Timeline.Endpoints.Dtos;

/// <summary>Activity stream entry.</summary>
public sealed record TimelineStreamEntryResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    TimelineStreamEntryType EntryType,
    string? AuthorId,
    string? AuthorName,
    string Body,
    IReadOnlyList<TimelineAttachmentInfoResponse> Attachments,
    Guid? ParentEntryId);

/// <summary>Attachment metadata.</summary>
public sealed record TimelineAttachmentInfoResponse(
    Guid Id,
    Guid BlobId,
    string FileName,
    string ContentType,
    long SizeBytes);
