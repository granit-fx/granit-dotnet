using Granit.Templating.Store;
using Granit.Workflow;
using Granit.Workflow.Domain;

namespace Granit.Templating.Workflow.Internal;

/// <summary>
/// Workflow-aware implementation of <see cref="ITemplateTransitionHook"/>.
/// Delegates transition validation to <see cref="IWorkflowManager{TState}"/>.
/// </summary>
/// <remarks>
/// Audit trail creation is handled by the <c>WorkflowTransitionInterceptor</c> automatically
/// when the <c>TemplateRevisionEntity</c> status changes are persisted. This hook only provides
/// pre-validation (permission checks and approval routing) via the workflow manager.
/// </remarks>
internal sealed class WorkflowTemplateTransitionHook(
    IWorkflowManager<WorkflowLifecycleStatus> workflowManager) : ITemplateTransitionHook
{
    /// <inheritdoc/>
    public bool IsWorkflowEnabled => true;

    /// <inheritdoc/>
    public async Task<bool> CanTransitionAsync(
        WorkflowLifecycleStatus from,
        WorkflowLifecycleStatus target,
        CancellationToken cancellationToken = default)
    {
        // Use TransitionAsync to validate both permission AND approval routing.
        // GetAllowedTransitionsAsync conflates "can execute" with "can request approval",
        // which would allow unpermitted users to publish directly.
        TransitionContext? context = WorkflowTransitionContext.Current is { } info
            ? new TransitionContext { Comment = info.Comment }
            : null;

        TransitionResult<WorkflowLifecycleStatus> result = await workflowManager
            .TransitionAsync(from, target, context, cancellationToken)
            .ConfigureAwait(false);

        // Only allow if the transition completed directly to the requested state.
        // ApprovalRequested means the user lacks permission -- the store must route
        // to PendingReview instead of the requested target.
        return result.Succeeded && result.Outcome == TransitionOutcome.Completed;
    }

    /// <inheritdoc/>
    public Task OnTransitionedAsync(
        Guid revisionId,
        WorkflowLifecycleStatus from,
        WorkflowLifecycleStatus target,
        string userId,
        CancellationToken cancellationToken = default)
    {
        // No-op: the WorkflowTransitionInterceptor automatically creates
        // WorkflowTransitionRecord entries when the entity status changes
        // are persisted via SaveChanges.
        return Task.CompletedTask;
    }
}
