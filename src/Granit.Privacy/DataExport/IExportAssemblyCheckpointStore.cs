namespace Granit.Privacy.DataExport;

/// <summary>
/// Persists the resumable state of an in-flight personal-data export assembly run,
/// keyed by request id + tenant. Granularity is the closed shard — a half-written
/// ZIP archive or an open multipart upload is not recoverable, so the next run
/// starts from the fragment that begins the shard after the last one fully
/// committed to storage.
/// </summary>
/// <remarks>
/// <para>
/// <b>Idempotent.</b> Setting a checkpoint replaces the prior value; clearing means
/// the assembly ran to completion and a re-dispatch would start over from scratch.
/// </para>
/// <para>
/// <b>Multi-tenant.</b> Implementations storing checkpoint rows MUST partition by
/// <c>TenantId</c> via <c>IMultiTenant</c> — see the EF impl in
/// <c>Granit.Privacy.EntityFrameworkCore</c> (shipped in a follow-up sub-PR).
/// </para>
/// <para>
/// <b>Concurrency.</b> One assembly job runs per request at a time. The store does
/// not need cross-process locking — the dispatcher's idempotency key on the saga
/// completion event prevents duplicate dispatch.
/// </para>
/// </remarks>
public interface IExportAssemblyCheckpointStore
{
    /// <summary>
    /// Returns the resumable checkpoint for the given (<paramref name="requestId"/>,
    /// <paramref name="tenantId"/>), or <see langword="null"/> when no prior run
    /// crashed and assembly should start from the first fragment.
    /// </summary>
    Task<ExportAssemblyCheckpoint?> GetAsync(
        Guid requestId,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Persists <paramref name="checkpoint"/> as the new resumable state.</summary>
    Task SetAsync(
        Guid requestId,
        Guid? tenantId,
        ExportAssemblyCheckpoint checkpoint,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the checkpoint after the assembly ran to completion. Subsequent calls
    /// to <see cref="GetAsync"/> return <see langword="null"/>.
    /// </summary>
    Task ClearAsync(
        Guid requestId,
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resumable state of an in-flight personal-data export assembly run.
/// </summary>
/// <param name="LastCompletedShardIndex">Index of the last shard whose multipart
/// upload was confirmed. <c>-1</c> when no shard has fully completed yet — the
/// next run resumes from fragment 0.</param>
/// <param name="NextFragmentIndex">Index into the event's <c>Fragments</c> list
/// where the next shard should resume. Always points past the last fragment that
/// was successfully appended to a fully-committed shard.</param>
/// <param name="CompletedShardObjectKeys">Object keys of the shards already
/// committed to blob storage, in shard-index order. Carried forward so the next
/// run can rebuild the manifest without rescanning the bucket.</param>
public sealed record ExportAssemblyCheckpoint(
    int LastCompletedShardIndex,
    int NextFragmentIndex,
    IReadOnlyList<string> CompletedShardObjectKeys);
