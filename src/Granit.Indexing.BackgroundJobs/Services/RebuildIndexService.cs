using Granit.Events;
using Granit.Indexing.BackgroundJobs.Diagnostics;
using Granit.Indexing.BackgroundJobs.Events;
using Granit.Indexing.BackgroundJobs.Exceptions;
using Granit.Indexing.BackgroundJobs.Options;
using Granit.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Indexing.BackgroundJobs.Services;

/// <summary>
/// Orchestrates a full rebuild for a single <typeparamref name="TKey"/>: reads the
/// last checkpoint, iterates the source from that point, indexes each entry through
/// the configured <see cref="IIndexer{TKey}"/>, and persists a new checkpoint every
/// <see cref="IndexingBackgroundJobsOptions.CheckpointBatchSize"/> entries.
/// </summary>
/// <remarks>
/// <para>
/// <b>Crash recovery.</b> When the worker dies mid-run, the next dispatch picks up the
/// checkpoint and resumes past it — the source MUST honour the
/// <c>resumeAfter</c> contract and emit keys strictly past the checkpoint, in the
/// same deterministic order.
/// </para>
/// <para>
/// <b>Cutoff conditions.</b> The run terminates and re-throws (Wolverine retry/DLQ
/// then takes over) when any of the following is hit:
/// <list type="bullet">
///   <item><see cref="IndexingBackgroundJobsOptions.MaxConsecutiveFailures"/> exceeded
///         — circuit-breaker against broken-data rollouts.</item>
///   <item><see cref="IndexingBackgroundJobsOptions.MaxEntriesPerRun"/> hit — budget
///         cap on per-run cost; raises <see cref="RebuildBudgetExceededException"/>.</item>
///   <item><see cref="IndexingBackgroundJobsOptions.MaxRunDurationSeconds"/> elapsed
///         — wall-clock budget; raises <see cref="RebuildBudgetExceededException"/>.</item>
///   <item>External cancellation — <see cref="OperationCanceledException"/> propagates.</item>
/// </list>
/// In every case the checkpoint is preserved before propagating so a follow-up dispatch
/// resumes from the last successful key — partial work is not wasted.
/// </para>
/// <para>
/// <b>Audit trail.</b> The service publishes <see cref="IndexRebuildStartedEvent"/>,
/// <see cref="IndexRebuildCompletedEvent"/>, and <see cref="IndexRebuildAbortedEvent"/>
/// on <see cref="ILocalEventBus"/>. Subscribers (typically <c>Granit.Auditing</c>)
/// persist a GDPR-grade processing log keyed off the dispatching user id when one is
/// associated with the scope.
/// </para>
/// </remarks>
public sealed partial class RebuildIndexService<TKey>
    where TKey : notnull
{
    private static readonly string KeyTypeName = typeof(TKey).FullName ?? typeof(TKey).Name;

    private readonly IIndexer<TKey> _indexer;
    private readonly IIndexedEntrySource<TKey> _source;
    private readonly IRebuildCheckpointStore<TKey> _checkpoints;
    private readonly IndexingBackgroundJobsOptions _options;
    private readonly IndexingBackgroundJobsMetrics _metrics;
    private readonly ILocalEventBus _eventBus;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RebuildIndexService<TKey>> _logger;

    public RebuildIndexService(
        IIndexer<TKey> indexer,
        IIndexedEntrySource<TKey> source,
        IRebuildCheckpointStore<TKey> checkpoints,
        IOptions<IndexingBackgroundJobsOptions> options,
        IndexingBackgroundJobsMetrics metrics,
        ILocalEventBus eventBus,
        ICurrentUserService currentUser,
        TimeProvider timeProvider,
        ILogger<RebuildIndexService<TKey>> logger)
    {
        ArgumentNullException.ThrowIfNull(indexer);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(checkpoints);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _indexer = indexer;
        _source = source;
        _checkpoints = checkpoints;
        _options = options.Value;
        _metrics = metrics;
        _eventBus = eventBus;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Runs a full rebuild for <paramref name="tenantId"/>.</summary>
    public async Task ExecuteAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        string tenantTag = tenantId?.ToString() ?? "global";
        string sourceName = _source.Name;
        string? dispatchedBy = _currentUser.UserId;

        TKey? checkpoint = await _checkpoints
            .GetLastCheckpointAsync(tenantId, sourceName, cancellationToken)
            .ConfigureAwait(false);

        // Quirk: with `TKey : notnull`, `TKey?` collapses to non-nullable `TKey` for
        // value-type instantiations, so `checkpoint is null` is always false. The
        // store's "no checkpoint" sentinel is therefore `default(TKey)` — see
        // InMemoryRebuildCheckpointStoreTests.GetLastCheckpointAsync_returns_default_when_no_checkpoint_set.
        bool resumed = !EqualityComparer<TKey?>.Default.Equals(checkpoint, default);
        if (resumed)
        {
            _metrics.RecordRebuildResumed(tenantTag, sourceName);
            LogResumed(tenantTag, sourceName);
        }
        else
        {
            _metrics.RecordRebuildStarted(tenantTag, sourceName);
            LogStarted(tenantTag, sourceName);
        }

        await _eventBus.PublishAsync(
            new IndexRebuildStartedEvent(tenantId, sourceName, KeyTypeName, resumed, dispatchedBy),
            cancellationToken).ConfigureAwait(false);

        // `ProcessedInBatch > 0` is the sole guard for checkpoint writes (a positive count
        // implies a loop body assigned `LastSuccessfulKey` before incrementing). We do NOT
        // gate on `LastSuccessfulKey is not null` because `TKey?` collapses to non-nullable
        // `TKey` under the `notnull` constraint for value-type instantiations.
        RebuildProgress progress = new() { LastSuccessfulKey = checkpoint };

        long startTimestamp = _timeProvider.GetTimestamp();
        TimeSpan? maxDuration = _options.MaxRunDurationSeconds is int s
            ? TimeSpan.FromSeconds(s)
            : null;
        RebuildRunContext context = new(tenantId, tenantTag, sourceName, dispatchedBy, startTimestamp, maxDuration);

        try
        {
            await foreach (TKey key in _source
                .EnumerateKeysAsync(tenantId, checkpoint, cancellationToken)
                .ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await IndexSingleKeyAsync(key, progress, context, cancellationToken).ConfigureAwait(false);
                await EnforceLimitsAsync(progress, context, cancellationToken).ConfigureAwait(false);
            }

            await _checkpoints.ClearAsync(tenantId, sourceName, cancellationToken).ConfigureAwait(false);
            _metrics.RecordRebuildCompleted(tenantTag, sourceName);
            LogCompleted(tenantTag, sourceName);

            await _eventBus.PublishAsync(
                new IndexRebuildCompletedEvent(
                    tenantId, sourceName, KeyTypeName, progress.Indexed, progress.Skipped, progress.Failed, dispatchedBy),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Persist progress on graceful cancellation so a re-dispatch resumes.
            if (progress.ProcessedInBatch > 0)
            {
                await _checkpoints
                    .SetCheckpointAsync(tenantId, sourceName, progress.LastSuccessfulKey!, CancellationToken.None)
                    .ConfigureAwait(false);
                _metrics.RecordCheckpointWritten(tenantTag, sourceName);
            }

            // Best-effort: cancellation may be racing event-bus shutdown, so use a fresh token.
            await _eventBus.PublishAsync(
                new IndexRebuildAbortedEvent(
                    tenantId, sourceName, KeyTypeName, "cancelled", progress.ProcessedTotal, dispatchedBy),
                CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    // Per-run mutable counters and the resume cursor.
    private sealed class RebuildProgress
    {
        public long Indexed;
        public long Skipped;
        public long Failed;
        public int ProcessedInBatch;
        public int ConsecutiveFailures;
        public TKey? LastSuccessfulKey;
        public long ProcessedTotal => Indexed + Skipped + Failed;
    }

    // Run-invariant context threaded through the per-key helpers.
    private readonly record struct RebuildRunContext(
        Guid? TenantId, string TenantTag, string SourceName, string? DispatchedBy,
        long StartTimestamp, TimeSpan? MaxDuration);

    // Builds and indexes one key, updating counters. Aborts (and rethrows) once the
    // consecutive-failure threshold is crossed; cancellation propagates untouched.
    private async Task IndexSingleKeyAsync(
        TKey key, RebuildProgress progress, RebuildRunContext ctx, CancellationToken cancellationToken)
    {
        try
        {
            IndexedEntry<TKey>? entry = await _source
                .BuildEntryAsync(key, cancellationToken)
                .ConfigureAwait(false);

            if (entry is null)
            {
                _metrics.RecordEntrySkipped(ctx.TenantTag, ctx.SourceName);
                progress.Skipped++;
            }
            else
            {
                await _indexer.IndexAsync(entry, cancellationToken).ConfigureAwait(false);
                _metrics.RecordEntryIndexed(ctx.TenantTag, ctx.SourceName);
                progress.Indexed++;
            }

            progress.LastSuccessfulKey = key;
            progress.ConsecutiveFailures = 0;
            progress.ProcessedInBatch++;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            progress.ConsecutiveFailures++;
            progress.Failed++;
            _metrics.RecordEntryFailed(ctx.TenantTag, ctx.SourceName, "build_or_index_error");
            LogEntryFailed(ctx.TenantTag, ctx.SourceName, ex.Message);

            if (progress.ConsecutiveFailures >= _options.MaxConsecutiveFailures)
            {
                await AbortAsync(
                    "max_consecutive_failures",
                    ctx.TenantId,
                    ctx.TenantTag,
                    ctx.SourceName,
                    progress.LastSuccessfulKey,
                    progress.ProcessedInBatch,
                    progress.ProcessedTotal,
                    ctx.DispatchedBy,
                    cancellationToken).ConfigureAwait(false);
                LogAborted(ctx.TenantTag, ctx.SourceName, progress.ConsecutiveFailures);
                throw;
            }
        }
    }

    // Writes a checkpoint once a batch fills, then enforces the per-run entry-count and
    // duration budgets — aborting (and throwing RebuildBudgetExceededException) when exceeded.
    private async Task EnforceLimitsAsync(
        RebuildProgress progress, RebuildRunContext ctx, CancellationToken cancellationToken)
    {
        if (progress.ProcessedInBatch >= _options.CheckpointBatchSize)
        {
            await _checkpoints
                .SetCheckpointAsync(ctx.TenantId, ctx.SourceName, progress.LastSuccessfulKey!, cancellationToken)
                .ConfigureAwait(false);
            _metrics.RecordCheckpointWritten(ctx.TenantTag, ctx.SourceName);
            progress.ProcessedInBatch = 0;
        }

        long processedTotal = progress.ProcessedTotal;

        if (_options.MaxEntriesPerRun is int maxEntries && processedTotal >= maxEntries)
        {
            await AbortAsync(
                "max_entries_per_run",
                ctx.TenantId,
                ctx.TenantTag,
                ctx.SourceName,
                progress.LastSuccessfulKey,
                progress.ProcessedInBatch,
                processedTotal,
                ctx.DispatchedBy,
                cancellationToken).ConfigureAwait(false);
            LogBudgetExceeded(ctx.TenantTag, ctx.SourceName, "max_entries_per_run", processedTotal);
            throw new RebuildBudgetExceededException("max_entries_per_run", ctx.TenantId, ctx.SourceName, processedTotal);
        }

        if (ctx.MaxDuration is TimeSpan budget && _timeProvider.GetElapsedTime(ctx.StartTimestamp) >= budget)
        {
            await AbortAsync(
                "max_run_duration",
                ctx.TenantId,
                ctx.TenantTag,
                ctx.SourceName,
                progress.LastSuccessfulKey,
                progress.ProcessedInBatch,
                processedTotal,
                ctx.DispatchedBy,
                cancellationToken).ConfigureAwait(false);
            LogBudgetExceeded(ctx.TenantTag, ctx.SourceName, "max_run_duration", processedTotal);
            throw new RebuildBudgetExceededException("max_run_duration", ctx.TenantId, ctx.SourceName, processedTotal);
        }
    }

    private async Task AbortAsync(
        string reason,
        Guid? tenantId,
        string tenantTag,
        string sourceName,
        TKey? lastSuccessfulKey,
        int processedInBatch,
        long entriesProcessed,
        string? dispatchedBy,
        CancellationToken cancellationToken)
    {
        if (processedInBatch > 0)
        {
            await _checkpoints
                .SetCheckpointAsync(tenantId, sourceName, lastSuccessfulKey!, cancellationToken)
                .ConfigureAwait(false);
            _metrics.RecordCheckpointWritten(tenantTag, sourceName);
        }

        _metrics.RecordRebuildAborted(tenantTag, sourceName);

        await _eventBus.PublishAsync(
            new IndexRebuildAbortedEvent(tenantId, sourceName, KeyTypeName, reason, entriesProcessed, dispatchedBy),
            cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Rebuild starting from beginning for tenant {TenantId} source {Source}")]
    private partial void LogStarted(string tenantId, string source);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rebuild resuming past checkpoint for tenant {TenantId} source {Source}")]
    private partial void LogResumed(string tenantId, string source);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rebuild completed for tenant {TenantId} source {Source}")]
    private partial void LogCompleted(string tenantId, string source);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rebuild entry failed for tenant {TenantId} source {Source}: {ErrorMessage}")]
    private partial void LogEntryFailed(string tenantId, string source, string errorMessage);

    [LoggerMessage(Level = LogLevel.Error, Message = "Rebuild aborted for tenant {TenantId} source {Source} after {ConsecutiveFailures} consecutive failures")]
    private partial void LogAborted(string tenantId, string source, int consecutiveFailures);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rebuild budget {Reason} exceeded for tenant {TenantId} source {Source} after {Processed} entries — checkpoint preserved for retry")]
    private partial void LogBudgetExceeded(string tenantId, string source, string reason, long processed);
}
