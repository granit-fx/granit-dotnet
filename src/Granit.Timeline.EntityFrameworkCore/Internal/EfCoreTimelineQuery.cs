using Granit.QueryEngine;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Timeline.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="ITimelineReader"/> backed by PostgreSQL.
/// </summary>
/// <remarks>
/// Queries use <c>AsNoTracking</c> for read performance. The soft-delete query filter
/// on <see cref="TimelineEntry"/> automatically excludes deleted entries.
/// </remarks>
internal sealed class EfCoreTimelineQuery(
    IDbContextFactory<TimelineDbContext> dbContextFactory) : ITimelineReader
{
    /// <inheritdoc/>
    public async Task<PagedResult<TimelineStreamEntry>> GetStreamAsync(
        string entityType,
        string entityId,
        int page = 1,
        int pageSize = QueryEngineDefaults.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        (int clampedPage, int clampedPageSize) = QueryEngineDefaults.ClampPagination(page, pageSize);

        await using TimelineDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        IQueryable<TimelineEntry> query = db.TimelineEntries
            .AsNoTracking()
            .Where(e => e.EntityType == entityType && e.EntityId == entityId);

        int totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        List<TimelineEntry> entries = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((clampedPage - 1) * clampedPageSize)
            .Take(clampedPageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var entryIds = entries.Select(e => e.Id).ToList();

        List<TimelineAttachment> attachments = await db.TimelineAttachments
            .AsNoTracking()
            .Where(a => entryIds.Contains(a.EntryId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        ILookup<Guid, TimelineAttachment> attachmentLookup = attachments.ToLookup(a => a.EntryId);

        var items = entries
            .Select(e => new TimelineStreamEntry
            {
                Id = e.Id,
                OccurredAt = e.CreatedAt,
                EntryType = e.EntryType switch
                {
                    TimelineEntryType.Comment => TimelineStreamEntryType.Comment,
                    TimelineEntryType.InternalNote => TimelineStreamEntryType.InternalNote,
                    TimelineEntryType.SystemLog => TimelineStreamEntryType.SystemLog,
                    _ => TimelineStreamEntryType.SystemLog,
                },
                AuthorId = e.AuthorId,
                AuthorName = e.AuthorName,
                Body = e.Body,
                ParentEntryId = e.ParentEntryId,
                Attachments = attachmentLookup[e.Id]
                    .Select(a => new TimelineAttachmentInfo(a.Id, a.BlobId, a.FileName, a.ContentType, a.SizeBytes))
                    .ToList(),
            })
            .ToList();

        int skip = (clampedPage - 1) * clampedPageSize;
        return new PagedResult<TimelineStreamEntry>(items, totalCount, HasMore: skip + items.Count < totalCount);
    }
}
