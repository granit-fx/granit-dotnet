using Granit.Privacy.DataExport.Events;

namespace Granit.Privacy.DataExport;

/// <summary>
/// Assembles the sharded ZIP archive for a single personal-data export request.
/// Implemented by <c>PrivacyExportAssemblyService</c> in
/// <c>Granit.Privacy.BlobStorage</c>; invoked from the
/// <c>PrivacyExportAssemblyJobHandler</c> in <c>Granit.Privacy.BackgroundJobs</c>.
/// </summary>
/// <remarks>
/// <para>
/// The contract sits on the base <c>Granit.Privacy</c> module so the background-job
/// package depends only on the high-level interface, not on the BlobStorage-specific
/// implementation — the host wires the impl by referencing
/// <c>Granit.Privacy.BlobStorage</c>.
/// </para>
/// <para>
/// Implementations MUST be tenant-aware: the caller opens the right
/// <see cref="Granit.MultiTenancy.ICurrentTenant"/> scope before invoking; the
/// implementation operates under that scope without rescoping.
/// </para>
/// </remarks>
public interface IPrivacyExportAssemblyService
{
    /// <summary>
    /// Assembles the export archive for <paramref name="completion"/>: verifies HMAC
    /// capabilities on every fragment, streams each into a sequence of size-capped
    /// ZIP shards via the configured blob provider, writes a checkpoint per
    /// completed shard, and updates the request tracker on success.
    /// </summary>
    Task AssembleAsync(ExportCompletedEto completion, CancellationToken cancellationToken);
}
