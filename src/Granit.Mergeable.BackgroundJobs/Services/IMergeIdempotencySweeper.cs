using Granit.Mergeable.EntityFrameworkCore.Options;

namespace Granit.Mergeable.BackgroundJobs.Services;

/// <summary>
/// Sweeps the <c>merge_idempotency</c> cache by deleting rows older than the
/// configured <c>MergeableOptions.IdempotencyRetention</c> window. Public surface so the
/// background-jobs handler (which must be public to be discovered by Wolverine) can depend
/// on it; the implementation lives behind an <c>internal</c> class that owns the internal
/// <c>MergeableDbContext</c>.
/// </summary>
public interface IMergeIdempotencySweeper
{
    /// <summary>
    /// Deletes every <c>MergeIdempotencyEntry</c> with <c>CreatedAt</c> older than
    /// <c>now - IdempotencyRetention</c>. Idempotent — re-running the sweep is safe.
    /// </summary>
    Task ExecuteAsync(CancellationToken cancellationToken);
}
