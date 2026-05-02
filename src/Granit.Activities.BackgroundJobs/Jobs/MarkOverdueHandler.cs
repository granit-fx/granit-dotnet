using Granit.Activities.BackgroundJobs.Services;

namespace Granit.Activities.BackgroundJobs.Jobs;

/// <summary>
/// Handles <see cref="MarkOverdueJob"/> by delegating to
/// <see cref="MarkOverdueScanService"/>.
/// </summary>
public class MarkOverdueHandler
{
    public static Task HandleAsync(
        MarkOverdueJob _,
        MarkOverdueScanService service,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(cancellationToken);
}
