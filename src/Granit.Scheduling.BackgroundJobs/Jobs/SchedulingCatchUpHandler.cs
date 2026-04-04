using Granit.Scheduling.BackgroundJobs.Internal;

namespace Granit.Scheduling.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="SchedulingCatchUpJob"/>. Delegates to <see cref="CatchUpDispatcher"/>
/// for overdue scheduled action detection and re-dispatch.
/// </summary>
internal static class SchedulingCatchUpHandler
{
    public static Task HandleAsync(
        SchedulingCatchUpJob _,
        CatchUpDispatcher dispatcher,
        CancellationToken cancellationToken) =>
        dispatcher.DispatchOverdueActionsAsync(cancellationToken);
}
