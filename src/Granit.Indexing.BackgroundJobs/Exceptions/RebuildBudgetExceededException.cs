namespace Granit.Indexing.BackgroundJobs.Exceptions;

/// <summary>
/// Thrown when a rebuild run exceeds <c>MaxEntriesPerRun</c> or
/// <c>MaxRunDurationSeconds</c>. The checkpoint is preserved before the exception is
/// raised so Wolverine's retry policy re-dispatches the job and resumes past the last
/// successfully processed key.
/// </summary>
/// <remarks>
/// Distinct from <see cref="RebuildAlreadyInProgressException"/>: this represents a
/// healthy budget cutoff (the run will be resumed), not a concurrency conflict
/// (the run is rejected).
/// </remarks>
public sealed class RebuildBudgetExceededException : Exception
{
    /// <summary>
    /// Stable cause identifier. One of <c>"max_entries_per_run"</c>,
    /// <c>"max_run_duration"</c>.
    /// </summary>
    public string Reason { get; }

    /// <summary>Tenant scope of the run that hit the budget.</summary>
    public Guid? TenantId { get; }

    /// <summary>Source name of the run that hit the budget.</summary>
    public string SourceName { get; }

    /// <summary>Number of entries processed (indexed + skipped + failed) before the cutoff.</summary>
    public long ProcessedCount { get; }

    public RebuildBudgetExceededException(string reason, Guid? tenantId, string sourceName, long processedCount)
        : base(
            $"Rebuild budget '{reason}' exceeded for source '{sourceName}' on tenant '{tenantId?.ToString() ?? "global"}' "
            + $"after {processedCount} entries — checkpoint preserved, retry will resume.")
    {
        ArgumentException.ThrowIfNullOrEmpty(reason);
        ArgumentException.ThrowIfNullOrEmpty(sourceName);
        Reason = reason;
        TenantId = tenantId;
        SourceName = sourceName;
        ProcessedCount = processedCount;
    }
}
