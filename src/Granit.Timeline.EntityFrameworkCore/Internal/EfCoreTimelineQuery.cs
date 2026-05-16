using Granit.QueryEngine;
using Granit.Timeline;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Domain;
using Granit.Timeline.Internal;
using Granit.Timeline.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Timeline.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="ITimelineReader"/> backed by PostgreSQL.
/// </summary>
/// <remarks>
/// Queries use <c>AsNoTracking</c> for read performance. The soft-delete query filter
/// on <see cref="TimelineEntry"/> automatically excludes deleted entries. Composition
/// with external <see cref="ITimelineSource"/> contributors is delegated to
/// <see cref="TimelineStreamMerger"/>.
/// </remarks>
internal sealed class EfCoreTimelineQuery(
    IDbContextFactory<TimelineDbContext> dbContextFactory,
    IEnumerable<ITimelineSource> sources,
    IOptions<TimelineOptions> options,
    ILogger<EfCoreTimelineQuery> logger) : ITimelineReader
{
    /// <inheritdoc/>
    public Task<TimelineStreamResult> GetStreamAsync(
        string entityType,
        string entityId,
        int page = 1,
        int pageSize = QueryEngineDefaults.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        TimelineStreamMerger.MergeAsync(
            entityType,
            entityId,
            page,
            pageSize,
            options.Value,
            sources,
            fetchNativeTopAsync: (limit, ct) => FetchNativeTopAsync(entityType, entityId, limit, ct),
            countNativeAsync: ct => CountNativeAsync(entityType, entityId, ct),
            logger,
            cancellationToken);

    private async Task<IReadOnlyList<TimelineStreamEntry>> FetchNativeTopAsync(
        string entityType,
        string entityId,
        int limit,
        CancellationToken cancellationToken)
    {
        await using TimelineDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        IQueryable<TimelineEntry> query = db.TimelineEntries
            .AsNoTracking()
            .Where(e => e.EntityType == entityType && e.EntityId == entityId);

        List<TimelineEntry> entries = await query
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (entries.Count == 0)
        {
            return [];
        }

        var entryIds = entries.Select(e => e.Id).ToList();

        List<TimelineAttachment> attachments = await db.TimelineAttachments
            .AsNoTracking()
            .Where(a => entryIds.Contains(a.EntryId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        ILookup<Guid, TimelineAttachment> attachmentLookup = attachments.ToLookup(a => a.EntryId);

        return [..
            entries.Select(e => new TimelineStreamEntry
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
                Origin = e.SourceKey is null ? TimelineEntryOrigin.Native : TimelineEntryOrigin.External,
                SourceKey = e.SourceKey ?? TimelineSourceKeys.Native,
                SourceId = e.SourceId,
                EditedAt = e.EditedAt,
                Attachments = [..
                    attachmentLookup[e.Id]
                        .Select(a => new TimelineAttachmentInfo(a.Id, a.BlobId, a.FileName, a.ContentType, a.SizeBytes))],
            })];
    }

    private async Task<int> CountNativeAsync(string entityType, string entityId, CancellationToken cancellationToken)
    {
        await using TimelineDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.TimelineEntries
            .AsNoTracking()
            .Where(e => e.EntityType == entityType && e.EntityId == entityId)
            .CountAsync(cancellationToken).ConfigureAwait(false);
    }
}
