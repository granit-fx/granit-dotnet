namespace Granit.Taxonomy;

/// <summary>
/// Nightly orphan-assignment sweep (T5.2): scans <c>TagAssignment</c> and
/// <c>CategoryAssignment</c> rows, asks the registered
/// <see cref="ITaggableExistenceProbe"/> per target type whether the underlying
/// aggregate still exists, and deletes the rows pointing at hard-deleted targets.
/// </summary>
/// <remarks>
/// Catches orphans that escaped the synchronous T5.1 cleanup — raw SQL deletes,
/// modules that never opted into <c>IEmitEntityLifecycleEvents</c>, and any
/// other path that bypasses the EF Core change tracker.
/// </remarks>
public interface IOrphanAssignmentSweepService
{
    /// <summary>Runs the sweep and returns the total number of assignment rows removed.</summary>
    Task<int> ExecuteAsync(CancellationToken cancellationToken);
}
