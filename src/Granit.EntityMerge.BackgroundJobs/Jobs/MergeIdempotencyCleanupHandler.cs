using Granit.EntityMerge.BackgroundJobs.Services;

namespace Granit.EntityMerge.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="MergeIdempotencyCleanupJob"/>. Delegates to
/// <see cref="IMergeIdempotencySweeper"/> for the actual sweep — keeps the handler trivial
/// and the implementation unit-testable without the Wolverine machinery.
/// </summary>
public class MergeIdempotencyCleanupHandler
{
    public static Task HandleAsync(
        MergeIdempotencyCleanupJob _,
        IMergeIdempotencySweeper sweeper,
        CancellationToken cancellationToken) =>
        sweeper.ExecuteAsync(cancellationToken);
}
