using Granit.MultiTenancy;
using Granit.Templating.Store;
using Granit.Workflow;
using Granit.Workflow.Domain;

namespace Granit.Templating.Workflow.Internal;

/// <summary>
/// Workflow-aware implementation of <see cref="ITemplateTransitionHook"/>.
/// Delegates transition validation to <see cref="IWorkflowManager{TState}"/> and
/// persists transition records via <see cref="IWorkflowTransitionRecorder"/> for unified ISO 27001 audit trail.
/// </summary>
internal sealed class WorkflowTemplateTransitionHook(
    IWorkflowManager<WorkflowLifecycleStatus> workflowManager,
    IWorkflowTransitionRecorder transitionRecorder,
    ICurrentTenant currentTenant) : ITemplateTransitionHook
{
    private const string EntityTypeName = "TemplateRevision";

    /// <inheritdoc/>
    public bool IsWorkflowEnabled => true;

    /// <inheritdoc/>
    public async Task<bool> CanTransitionAsync(
        TemplateLifecycleStatus from,
        TemplateLifecycleStatus target,
        CancellationToken cancellationToken = default)
    {
        WorkflowLifecycleStatus wFrom = ToWorkflow(from);
        WorkflowLifecycleStatus wTo = ToWorkflow(target);

        // Use TransitionAsync to validate both permission AND approval routing.
        // GetAllowedTransitionsAsync conflates "can execute" with "can request approval",
        // which would allow unpermitted users to publish directly.
        TransitionContext? context = WorkflowTransitionContext.Current is { } info
            ? new TransitionContext { Comment = info.Comment }
            : null;

        TransitionResult<WorkflowLifecycleStatus> result = await workflowManager
            .TransitionAsync(wFrom, wTo, context, cancellationToken)
            .ConfigureAwait(false);

        // Only allow if the transition completed directly to the requested state.
        // ApprovalRequested means the user lacks permission — the store must route
        // to PendingReview instead of the requested target.
        return result.Succeeded && result.Outcome == TransitionOutcome.Completed;
    }

    /// <inheritdoc/>
    public async Task OnTransitionedAsync(
        Guid revisionId,
        TemplateLifecycleStatus from,
        TemplateLifecycleStatus target,
        string userId,
        CancellationToken cancellationToken = default)
    {
        await transitionRecorder.RecordTransitionAsync(
            new RecordTransitionRequest
            {
                EntityType = EntityTypeName,
                EntityId = revisionId.ToString(),
                PreviousState = from.ToString(),
                NewState = target.ToString(),
                UserId = userId,
                Comment = WorkflowTransitionContext.Current?.Comment,
                TenantId = currentTenant.IsAvailable ? currentTenant.Id : null,
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static WorkflowLifecycleStatus ToWorkflow(TemplateLifecycleStatus status) =>
        (WorkflowLifecycleStatus)(int)status;
}
