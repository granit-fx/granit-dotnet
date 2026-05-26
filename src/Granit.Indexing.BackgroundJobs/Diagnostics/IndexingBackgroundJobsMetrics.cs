using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Indexing.BackgroundJobs.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the indexing background-jobs module. Meter:
/// <c>Granit.Indexing.BackgroundJobs</c>.
/// </summary>
/// <remarks>
/// All counters carry a <c>tenant_id</c> tag (coalesced to <c>"global"</c>) and a
/// <c>source</c> tag (<see cref="IIndexedEntrySource{TKey}.Name"/>). Per-key content
/// is NEVER tagged.
/// </remarks>
public sealed class IndexingBackgroundJobsMetrics
{
    public const string MeterName = "Granit.Indexing.BackgroundJobs";

    private readonly Counter<long> _rebuildStarted;
    private readonly Counter<long> _rebuildCompleted;
    private readonly Counter<long> _rebuildResumed;
    private readonly Counter<long> _rebuildAborted;
    private readonly Counter<long> _entriesIndexed;
    private readonly Counter<long> _entriesSkipped;
    private readonly Counter<long> _entriesFailed;
    private readonly Counter<long> _checkpointsWritten;

    public IndexingBackgroundJobsMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter meter = meterFactory.Create(MeterName);

        _rebuildStarted = meter.CreateCounter<long>(
            "granit.indexing.background_jobs.rebuild.started",
            description: "Number of rebuild runs that started from the beginning (no prior checkpoint).");

        _rebuildResumed = meter.CreateCounter<long>(
            "granit.indexing.background_jobs.rebuild.resumed",
            description: "Number of rebuild runs that resumed past a checkpoint.");

        _rebuildCompleted = meter.CreateCounter<long>(
            "granit.indexing.background_jobs.rebuild.completed",
            description: "Number of rebuild runs that reached end of stream and cleared their checkpoint.");

        _rebuildAborted = meter.CreateCounter<long>(
            "granit.indexing.background_jobs.rebuild.aborted",
            description: "Number of rebuild runs aborted because MaxConsecutiveFailures was exceeded.");

        _entriesIndexed = meter.CreateCounter<long>(
            "granit.indexing.background_jobs.entries.indexed",
            description: "Per-entry indexing successes during a rebuild.");

        _entriesSkipped = meter.CreateCounter<long>(
            "granit.indexing.background_jobs.entries.skipped",
            description: "Per-entry skips during a rebuild (resource no longer exists at source).");

        _entriesFailed = meter.CreateCounter<long>(
            "granit.indexing.background_jobs.entries.failed",
            description: "Per-entry failures during a rebuild (source build error or indexer fault).");

        _checkpointsWritten = meter.CreateCounter<long>(
            "granit.indexing.background_jobs.checkpoints.written",
            description: "Number of checkpoint writes — one per CheckpointBatchSize processed entries.");
    }

    public void RecordRebuildStarted(string? tenantId, string sourceName) =>
        Bump(_rebuildStarted, tenantId, sourceName);

    public void RecordRebuildResumed(string? tenantId, string sourceName) =>
        Bump(_rebuildResumed, tenantId, sourceName);

    public void RecordRebuildCompleted(string? tenantId, string sourceName) =>
        Bump(_rebuildCompleted, tenantId, sourceName);

    public void RecordRebuildAborted(string? tenantId, string sourceName) =>
        Bump(_rebuildAborted, tenantId, sourceName);

    public void RecordEntryIndexed(string? tenantId, string sourceName) =>
        Bump(_entriesIndexed, tenantId, sourceName);

    public void RecordEntrySkipped(string? tenantId, string sourceName) =>
        Bump(_entriesSkipped, tenantId, sourceName);

    public void RecordEntryFailed(string? tenantId, string sourceName, string reason)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("source", sourceName),
            new("reason", reason),
        ];
        _entriesFailed.Add(1, tags);
    }

    public void RecordCheckpointWritten(string? tenantId, string sourceName) =>
        Bump(_checkpointsWritten, tenantId, sourceName);

    private static void Bump(Counter<long> counter, string? tenantId, string sourceName)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("source", sourceName),
        ];
        counter.Add(1, tags);
    }
}
