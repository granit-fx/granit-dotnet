using Granit.Workflow.Domain;

namespace Granit.Templating.Store;

/// <summary>
/// Extension point for template lifecycle transitions.
/// </summary>
public interface ITemplateTransitionHook
{
    /// <summary>Whether the Workflow module is providing the hook implementation.</summary>
    bool IsWorkflowEnabled { get; }

    /// <summary>Validates whether a lifecycle transition is allowed.</summary>
    Task<bool> CanTransitionAsync(
        WorkflowLifecycleStatus from,
        WorkflowLifecycleStatus target,
        CancellationToken cancellationToken = default);

    /// <summary>Invoked after a lifecycle transition has been persisted.</summary>
    Task OnTransitionedAsync(
        Guid revisionId,
        WorkflowLifecycleStatus from,
        WorkflowLifecycleStatus target,
        string userId,
        CancellationToken cancellationToken = default);
}
