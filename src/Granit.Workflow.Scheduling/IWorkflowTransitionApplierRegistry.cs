namespace Granit.Workflow.Scheduling;

/// <summary>
/// Resolves the <see cref="IWorkflowTransitionApplier"/> registered for a given workflow entity
/// type. Used by <see cref="ScheduledWorkflowTransitionHandler"/> to dispatch a scheduled
/// transition to the correct entity-specific applier.
/// </summary>
public interface IWorkflowTransitionApplierRegistry
{
    /// <summary>
    /// Returns the applier registered under <paramref name="workflowEntityType"/>.
    /// </summary>
    /// <param name="workflowEntityType">
    /// The logical workflow entity type key (matching
    /// <c>ScheduledWorkflowTransitionPayload.WorkflowEntityType</c>).
    /// </param>
    /// <returns>The registered applier.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no applier is registered for <paramref name="workflowEntityType"/>.
    /// </exception>
    IWorkflowTransitionApplier Resolve(string workflowEntityType);
}
