using Granit.QueryEngine;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Domain;

namespace Granit.Timeline.Internal;

/// <summary>
/// In-memory implementation of <see cref="ITimelineReader"/> for development and tests.
/// </summary>
internal sealed class InMemoryTimelineQuery(InMemoryTimelineStore store) : ITimelineReader
{
    /// <inheritdoc/>
    public Task<PagedResult<TimelineStreamEntry>> GetStreamAsync(
        string entityType,
        string entityId,
        int page = 1,
        int pageSize = QueryEngineDefaults.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        (int clampedPage, int clampedPageSize) = QueryEngineDefaults.ClampPagination(page, pageSize);

        var entries = store.Entries.Values
            .Where(e => e.EntityType == entityType
                        && e.EntityId == entityId
                        && !e.IsDeleted)
            .OrderByDescending(e => e.CreatedAt)
            .ToList();

        int totalCount = entries.Count;

        var items = entries
            .Skip((clampedPage - 1) * clampedPageSize)
            .Take(clampedPageSize)
            .Select(MapToStreamEntry)
            .ToList();

        int skip = (clampedPage - 1) * clampedPageSize;
        return Task.FromResult(new PagedResult<TimelineStreamEntry>(items, totalCount, HasMore: skip + items.Count < totalCount));
    }

    private TimelineStreamEntry MapToStreamEntry(TimelineEntry entry) =>
        new()
        {
            Id = entry.Id,
            OccurredAt = entry.CreatedAt,
            EntryType = entry.EntryType switch
            {
                TimelineEntryType.Comment => TimelineStreamEntryType.Comment,
                TimelineEntryType.InternalNote => TimelineStreamEntryType.InternalNote,
                TimelineEntryType.SystemLog => TimelineStreamEntryType.SystemLog,
                _ => TimelineStreamEntryType.SystemLog,
            },
            AuthorId = entry.AuthorId,
            AuthorName = entry.AuthorName,
            Body = entry.Body,
            ParentEntryId = entry.ParentEntryId,
            Attachments = store.Attachments.Values
                .Where(a => a.EntryId == entry.Id)
                .Select(a => new TimelineAttachmentInfo(a.Id, a.BlobId, a.FileName, a.ContentType, a.SizeBytes))
                .ToList(),
        };
}
