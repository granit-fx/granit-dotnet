using Granit.Privacy.BackgroundJobs.Services;

namespace Granit.Privacy.BackgroundJobs.Jobs;

/// <summary>
/// Handles <see cref="DeletionDeadlineEnforcerJob"/> by delegating to
/// <see cref="DeletionDeadlineEnforcementService"/> for expired deferred deletion processing.
/// </summary>
public class DeletionDeadlineEnforcerHandler
{
    public static Task HandleAsync(
        DeletionDeadlineEnforcerJob _,
        DeletionDeadlineEnforcementService service,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(cancellationToken);
}
