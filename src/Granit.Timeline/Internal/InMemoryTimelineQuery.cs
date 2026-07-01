using Granit.QueryEngine;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Domain;
using Granit.Timeline.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Granit.Timeline.Internal;

/// <summary>
/// In-memory implementation of <see cref="ITimelineReader"/> for development and tests.
/// </summary>
internal sealed class InMemoryTimelineQuery(
    InMemoryTimelineStore store,
    IEnumerable<ITimelineSource> sources,
    IOptions<TimelineOptions> options,
    ILogger<InMemoryTimelineQuery>? logger = null) : ITimelineReader
{
    private readonly ILogger _logger = logger ?? NullLogger<InMemoryTimelineQuery>.Instance;

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
            fetchNativeTopAsync: (limit, _) => Task.FromResult<IReadOnlyList<TimelineStreamEntry>>([..
                store.Entries.Values
                    .Where(e => e.EntityType == entityType && e.EntityId == entityId && !e.IsDeleted)
                    .OrderByDescending(e => e.CreatedAt)
                    .Take(limit)
                    .Select(MapToStreamEntry)]),
            countNativeAsync: _ => Task.FromResult(
                store.Entries.Values.Count(e => e.EntityType == entityType && e.EntityId == entityId && !e.IsDeleted)),
            _logger,
            cancellationToken);

    /// <inheritdoc/>
    public Task<TimelineStreamEntry?> GetEntryAsync(
        string entityType,
        string entityId,
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        TimelineStreamEntry? entry =
            store.Entries.TryGetValue(entryId, out TimelineEntry? e)
                && e.EntityType == entityType && e.EntityId == entityId && !e.IsDeleted
                ? MapToStreamEntry(e)
                : null;
        return Task.FromResult(entry);
    }

    /// <inheritdoc/>
    public Task<TimelineEntry?> GetByIdAsync(
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        TimelineEntry? entry = store.Entries.TryGetValue(entryId, out TimelineEntry? e) && !e.IsDeleted
            ? e
            : null;
        return Task.FromResult(entry);
    }

    private TimelineStreamEntry MapToStreamEntry(TimelineEntry entry) =>
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
                store.Attachments.Values
                    .Where(a => a.EntryId == entry.Id)
                    .Select(a => new TimelineAttachmentInfo(a.Id, a.BlobId, a.FileName, a.ContentType, a.SizeBytes))],
        };
}
