namespace Granit.Indexing.BackgroundJobs;

/// <summary>
/// Persists the last successfully processed key for a (tenant, source) tuple so the
/// rebuild job can resume past it after a crash or restart.
/// </summary>
/// <remarks>
/// <para>
/// <b>Idempotent.</b> Setting a checkpoint replaces the prior value; clearing means
/// "next run starts from the beginning". Re-reading the same checkpoint twice returns
/// the same value as long as no writer has updated it in between.
/// </para>
/// <para>
/// <b>Concurrency.</b> Only one rebuild job runs at a time per (tenant, source); the
/// store doesn't need cross-process locking. Hosts that run multiple workers MUST gate
/// the dispatcher itself (Wolverine's <c>UseDurableInbox</c> + a stable job ID
/// suffices).
/// </para>
/// </remarks>
/// <typeparam name="TKey">Resource primary key.</typeparam>
public interface IRebuildCheckpointStore<TKey>
{
    /// <summary>
    /// Returns the last checkpoint for the given (<paramref name="tenantId"/>,
    /// <paramref name="sourceName"/>), or <c>null</c> when none was ever set or the
    /// last run completed successfully (which clears the row).
    /// </summary>
    Task<TKey?> GetLastCheckpointAsync(
        Guid? tenantId,
        string sourceName,
        CancellationToken cancellationToken = default);

    /// <summary>Persists <paramref name="checkpoint"/> as the new last-processed key.</summary>
    Task SetCheckpointAsync(
        Guid? tenantId,
        string sourceName,
        TKey checkpoint,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the checkpoint after a clean run-to-completion. Subsequent calls to
    /// <see cref="GetLastCheckpointAsync"/> return <c>null</c> until the next
    /// <see cref="SetCheckpointAsync"/>.
    /// </summary>
    Task ClearAsync(
        Guid? tenantId,
        string sourceName,
        CancellationToken cancellationToken = default);
}
