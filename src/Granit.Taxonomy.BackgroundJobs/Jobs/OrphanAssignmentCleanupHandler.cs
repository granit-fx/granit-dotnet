namespace Granit.Taxonomy.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="OrphanAssignmentCleanupJob"/>. Delegates to
/// <see cref="IOrphanAssignmentSweepService"/> for the actual sweep logic.
/// </summary>
public class OrphanAssignmentCleanupHandler
{
    public static Task HandleAsync(
        OrphanAssignmentCleanupJob _,
        IOrphanAssignmentSweepService sweepService,
        CancellationToken cancellationToken) =>
        sweepService.ExecuteAsync(cancellationToken);
}
