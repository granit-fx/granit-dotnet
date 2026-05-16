using Granit.QueryEngine;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Exceptions;
using Granit.Timeline.Options;
using Microsoft.Extensions.Logging;

namespace Granit.Timeline.Internal;

/// <summary>
/// Federated read pipeline: fetches the native top-K via the supplied
/// delegate, fans out to every registered <see cref="ITimelineSource"/> under
/// per-source timeouts, deduplicates shadow rows, applies the canonical sort,
/// and slices the requested page. Centralised so the in-memory and EF Core
/// readers share identical semantics.
/// </summary>
/// <remarks>
/// Cancellation/timeout protocol:
/// <list type="bullet">
///   <item>The caller's <see cref="CancellationToken"/> aborts the whole pipeline.</item>
///   <item>Each <c>ITimelineSource</c> call is wrapped in
///         <see cref="Task.WaitAsync(TimeSpan, CancellationToken)"/> with
///         <see cref="TimelineOptions.SourceTimeout"/>. A timeout is recorded
///         as a degraded source under <see cref="SourceFailurePolicy.DegradeGracefully"/>
///         and rethrown under <see cref="SourceFailurePolicy.ThrowAll"/>.</item>
///   <item>Native fetch failures always propagate — the native store is not
///         degradable; if it dies, the response is meaningless.</item>
/// </list>
/// </remarks>
internal static class TimelineStreamMerger
{
    public static async Task<TimelineStreamResult> MergeAsync(
        string entityType,
        string entityId,
        int page,
        int pageSize,
        TimelineOptions options,
        IEnumerable<ITimelineSource> sources,
        Func<int, CancellationToken, Task<IReadOnlyList<TimelineStreamEntry>>> fetchNativeTopAsync,
        Func<CancellationToken, Task<int>> countNativeAsync,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (page > options.MaxPage)
        {
            throw new TimelineDepthExceededException(page, options.MaxPage);
        }

        (int clampedPage, int clampedPageSize) = QueryEngineDefaults.ClampPagination(page, pageSize);
        int fetchLimit = (clampedPage * clampedPageSize) + clampedPageSize; // buffer = one page

        Task<IReadOnlyList<TimelineStreamEntry>> nativeTask = fetchNativeTopAsync(fetchLimit, cancellationToken);

        List<ITimelineSource> sourceList = [.. sources];
        var sourceTasks = sourceList
            .Select(s => FetchSourceAsync(s, entityType, entityId, fetchLimit, options, logger, cancellationToken))
            .ToList();

        IReadOnlyList<TimelineStreamEntry> nativeEntries = await nativeTask.ConfigureAwait(false);
        SourceFetch[] sourceResults = await Task.WhenAll(sourceTasks).ConfigureAwait(false);

        List<string> degraded = [.. sourceResults.Where(r => r.Degraded).Select(r => r.SourceKey)];

        // Native shadow rows preempt the matching external entry — same
        // SourceKey + SourceId, but the native row carries reactions/edits.
        HashSet<(string SourceKey, string SourceId)> shadowedKeys = [..
            nativeEntries
                .Where(e => e.SourceKey != TimelineSourceKeys.Native && e.SourceId is not null)
                .Select(e => (e.SourceKey, e.SourceId!))];

        IEnumerable<TimelineStreamEntry> externalEntries = sourceResults
            .Where(r => !r.Degraded)
            .SelectMany(r => r.Entries)
            .Where(e => e.SourceId is null || !shadowedKeys.Contains((e.SourceKey, e.SourceId)));

        List<TimelineStreamEntry> merged = [.. nativeEntries.Concat(externalEntries)];

        // Canonical sort: newest first, native wins ties, then SourceKey ASC, then Id ASC.
        merged.Sort(static (a, b) =>
        {
            int byTime = b.OccurredAt.CompareTo(a.OccurredAt);
            if (byTime != 0)
            {
                return byTime;
            }

            int byOrigin = (a.Origin == TimelineEntryOrigin.Native ? 0 : 1)
                         - (b.Origin == TimelineEntryOrigin.Native ? 0 : 1);
            if (byOrigin != 0)
            {
                return byOrigin;
            }

            int bySource = string.CompareOrdinal(a.SourceKey, b.SourceKey);
            return bySource != 0 ? bySource : a.Id.CompareTo(b.Id);
        });

        int skip = (clampedPage - 1) * clampedPageSize;
        List<TimelineStreamEntry> pageItems = [.. merged.Skip(skip).Take(clampedPageSize)];

        // Total count is best-effort: native exact + sources contribute their fetched window.
        // Truly accurate totals would require a count call per source — deferred until a
        // real product need shows up. The HasMore flag remains correct because it's
        // computed from the merged buffer length, not the totals.
        int nativeTotal = await countNativeAsync(cancellationToken).ConfigureAwait(false);
        int sourceTotal = sourceResults.Where(r => !r.Degraded).Sum(r => r.Entries.Count);
        int approxTotal = nativeTotal + sourceTotal;

        bool hasMore = merged.Count > skip + pageItems.Count;
        PagedResult<TimelineStreamEntry> pageResult = new(pageItems, approxTotal, HasMore: hasMore);

        return new TimelineStreamResult(pageResult, degraded);
    }

    private static async Task<SourceFetch> FetchSourceAsync(
        ITimelineSource source,
        string entityType,
        string entityId,
        int limit,
        TimelineOptions options,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            Task<IReadOnlyList<TimelineStreamEntry>> task = source
                .GetEntriesAsync(entityType, entityId, limit, cancellationToken);

            IReadOnlyList<TimelineStreamEntry> entries = await task
                .WaitAsync(options.SourceTimeout, cancellationToken)
                .ConfigureAwait(false);

            return new SourceFetch(source.SourceKey, entries, Degraded: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (options.OnSourceFailure == SourceFailurePolicy.DegradeGracefully)
        {
            logger.LogWarning(ex,
                "Timeline source {SourceKey} failed for {EntityType}/{EntityId}; degrading gracefully.",
                source.SourceKey, entityType, entityId);
            return new SourceFetch(source.SourceKey, [], Degraded: true);
        }
    }

    private readonly record struct SourceFetch(string SourceKey, IReadOnlyList<TimelineStreamEntry> Entries, bool Degraded);
}
