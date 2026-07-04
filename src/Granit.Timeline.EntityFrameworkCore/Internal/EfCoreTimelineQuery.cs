using Granit.QueryEngine;
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

        List<Guid> entryIds = entries.ConvertAll(e => e.Id);

        List<TimelineAttachment> attachments = await db.TimelineAttachments
            .AsNoTracking()
            .Where(a => entryIds.Contains(a.EntryId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        ILookup<Guid, TimelineAttachment> attachmentLookup = attachments.ToLookup(a => a.EntryId);

        return [.. entries.Select(e => MapToStreamEntry(e, attachmentLookup[e.Id]))];
    }

    /// <inheritdoc/>
    public async Task<TimelineStreamEntry?> GetEntryAsync(
        string entityType,
        string entityId,
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        await using TimelineDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        TimelineEntry? entry = await db.TimelineEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.Id == entryId && e.EntityType == entityType && e.EntityId == entityId,
                cancellationToken).ConfigureAwait(false);

        if (entry is null)
        {
            return null;
        }

        List<TimelineAttachment> attachments = await db.TimelineAttachments
            .AsNoTracking()
            .Where(a => a.EntryId == entryId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return MapToStreamEntry(entry, attachments);
    }

    /// <inheritdoc/>
    public async Task<TimelineEntry?> GetByIdAsync(
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        await using TimelineDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await db.TimelineEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == entryId, cancellationToken).ConfigureAwait(false);
    }

    private static TimelineStreamEntry MapToStreamEntry(
        TimelineEntry entry, IEnumerable<TimelineAttachment> attachments) =>
        new()
        {
            Id = entry.Id,
            OccurredAt = entry.CreatedAt,
            EntryType = (TimelineStreamEntryType)entry.EntryType,
            AuthorId = entry.AuthorId,
            AuthorName = entry.AuthorName,
            Body = entry.Body,
            ParentEntryId = entry.ParentEntryId,
            Origin = entry.SourceKey is null ? TimelineEntryOrigin.Native : TimelineEntryOrigin.External,
            SourceKey = entry.SourceKey ?? TimelineSourceKeys.Native,
            SourceId = entry.SourceId,
            EditedAt = entry.EditedAt,
            Attachments = [..
                attachments.Select(a => new TimelineAttachmentInfo(a.Id, a.BlobId, a.FileName, a.ContentType, a.SizeBytes))],
        };

    private async Task<int> CountNativeAsync(string entityType, string entityId, CancellationToken cancellationToken)
    {
        await using TimelineDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.TimelineEntries
            .AsNoTracking()
            .Where(e => e.EntityType == entityType && e.EntityId == entityId)
            .CountAsync(cancellationToken).ConfigureAwait(false);
    }
}
