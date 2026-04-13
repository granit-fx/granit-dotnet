using Granit.Timeline;
using Granit.Timeline.Endpoints.Dtos;

namespace Granit.Timeline.Endpoints.Internal;

internal static class TimelineResponseMapper
{
    internal static TimelineStreamEntryResponse ToResponse(TimelineStreamEntry entry) =>
        new(entry.Id, entry.OccurredAt, entry.EntryType, entry.AuthorId, entry.AuthorName, entry.Body,
            entry.Attachments.Select(ToResponse).ToList(), entry.ParentEntryId);

    internal static TimelineAttachmentInfoResponse ToResponse(TimelineAttachmentInfo attachment) =>
        new(attachment.Id, attachment.BlobId, attachment.FileName, attachment.ContentType, attachment.SizeBytes);
}
