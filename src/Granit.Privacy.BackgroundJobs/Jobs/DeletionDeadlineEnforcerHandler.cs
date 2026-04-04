using Granit.Privacy.BackgroundJobs.Internal;

namespace Granit.Privacy.BackgroundJobs.Jobs;

/// <summary>
/// Handles <see cref="DeletionDeadlineEnforcerJob"/> by delegating to
/// <see cref="DeletionDeadlineEnforcementService"/> for expired deferred deletion processing.
/// </summary>
internal static class DeletionDeadlineEnforcerHandler
{
    public static Task HandleAsync(
        DeletionDeadlineEnforcerJob _,
        DeletionDeadlineEnforcementService service,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(cancellationToken);
}
