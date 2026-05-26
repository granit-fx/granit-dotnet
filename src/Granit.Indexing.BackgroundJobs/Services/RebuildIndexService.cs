using Granit.Indexing.BackgroundJobs.Diagnostics;
using Granit.Indexing.BackgroundJobs.Options;
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
/// <b>Run abort.</b> If <see cref="IndexingBackgroundJobsOptions.MaxConsecutiveFailures"/>
/// is exceeded the service aborts the run, records the metric, and re-throws so
/// Wolverine's retry/DLQ policy kicks in. The checkpoint is preserved so a manual
/// follow-up trigger can resume — partial work is not wasted.
/// </para>
/// </remarks>
public sealed partial class RebuildIndexService<TKey>
    where TKey : notnull
{
    private readonly IIndexer<TKey> _indexer;
    private readonly IIndexedEntrySource<TKey> _source;
    private readonly IRebuildCheckpointStore<TKey> _checkpoints;
    private readonly IndexingBackgroundJobsOptions _options;
    private readonly IndexingBackgroundJobsMetrics _metrics;
    private readonly ILogger<RebuildIndexService<TKey>> _logger;

    public RebuildIndexService(
        IIndexer<TKey> indexer,
        IIndexedEntrySource<TKey> source,
        IRebuildCheckpointStore<TKey> checkpoints,
        IOptions<IndexingBackgroundJobsOptions> options,
        IndexingBackgroundJobsMetrics metrics,
        ILogger<RebuildIndexService<TKey>> logger)
    {
        ArgumentNullException.ThrowIfNull(indexer);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(checkpoints);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(logger);
        _indexer = indexer;
        _source = source;
        _checkpoints = checkpoints;
        _options = options.Value;
        _metrics = metrics;
        _logger = logger;
    }

    /// <summary>Runs a full rebuild for <paramref name="tenantId"/>.</summary>
    public async Task ExecuteAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        string tenantTag = tenantId?.ToString() ?? "global";
        string sourceName = _source.Name;

        TKey? checkpoint = await _checkpoints
            .GetLastCheckpointAsync(tenantId, sourceName, cancellationToken)
            .ConfigureAwait(false);

        if (checkpoint is null)
        {
            _metrics.RecordRebuildStarted(tenantTag, sourceName);
            LogStarted(tenantTag, sourceName);
        }
        else
        {
            _metrics.RecordRebuildResumed(tenantTag, sourceName);
            LogResumed(tenantTag, sourceName);
        }

        int processedInBatch = 0;
        int consecutiveFailures = 0;
        TKey? lastSuccessfulKey = checkpoint;

        try
        {
            await foreach (TKey key in _source
                .EnumerateKeysAsync(tenantId, checkpoint, cancellationToken)
                .ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    IndexedEntry<TKey>? entry = await _source
                        .BuildEntryAsync(key, cancellationToken)
                        .ConfigureAwait(false);

                    if (entry is null)
                    {
                        _metrics.RecordEntrySkipped(tenantTag, sourceName);
                    }
                    else
                    {
                        await _indexer.IndexAsync(entry, cancellationToken).ConfigureAwait(false);
                        _metrics.RecordEntryIndexed(tenantTag, sourceName);
                    }

                    lastSuccessfulKey = key;
                    consecutiveFailures = 0;
                    processedInBatch++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    consecutiveFailures++;
                    _metrics.RecordEntryFailed(tenantTag, sourceName, "build_or_index_error");
                    LogEntryFailed(tenantTag, sourceName, ex.Message);

                    if (consecutiveFailures >= _options.MaxConsecutiveFailures)
                    {
                        _metrics.RecordRebuildAborted(tenantTag, sourceName);
                        LogAborted(tenantTag, sourceName, consecutiveFailures);
                        throw;
                    }
                }

                if (processedInBatch >= _options.CheckpointBatchSize && lastSuccessfulKey is not null)
                {
                    await _checkpoints
                        .SetCheckpointAsync(tenantId, sourceName, lastSuccessfulKey, cancellationToken)
                        .ConfigureAwait(false);
                    _metrics.RecordCheckpointWritten(tenantTag, sourceName);
                    processedInBatch = 0;
                }
            }

            await _checkpoints.ClearAsync(tenantId, sourceName, cancellationToken).ConfigureAwait(false);
            _metrics.RecordRebuildCompleted(tenantTag, sourceName);
            LogCompleted(tenantTag, sourceName);
        }
        catch (OperationCanceledException)
        {
            // Persist progress on graceful cancellation so a re-dispatch resumes.
            if (lastSuccessfulKey is not null && processedInBatch > 0)
            {
                await _checkpoints
                    .SetCheckpointAsync(tenantId, sourceName, lastSuccessfulKey, CancellationToken.None)
                    .ConfigureAwait(false);
                _metrics.RecordCheckpointWritten(tenantTag, sourceName);
            }
            throw;
        }
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
}
