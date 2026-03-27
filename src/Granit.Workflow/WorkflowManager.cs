using System.Diagnostics;
using Granit.MultiTenancy;
using Granit.Workflow.Diagnostics;

namespace Granit.Workflow;

/// <summary>
/// Default implementation of <see cref="IWorkflowManager{TState}"/>.
/// Orchestrates transitions with permission checking and approval routing.
/// </summary>
/// <typeparam name="TState">Enum type representing the workflow states.</typeparam>
public sealed class WorkflowManager<TState>(
    IWorkflowDefinition<TState> definition,
    IWorkflowPermissionChecker permissionChecker,
    WorkflowMetrics metrics,
    ICurrentTenant currentTenant) : IWorkflowManager<TState>
    where TState : struct, Enum
{
    private readonly IWorkflowDefinition<TState> _definition = definition;
    private readonly IWorkflowPermissionChecker _permissionChecker = permissionChecker;
    private readonly WorkflowMetrics _metrics = metrics;
    private readonly ICurrentTenant _currentTenant = currentTenant;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WorkflowTransition<TState>>> GetAllowedTransitionsAsync(
        TState currentState,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WorkflowTransition<TState>> allTransitions = _definition.GetAllowedTransitions(currentState);
        List<WorkflowTransition<TState>> allowed = new(allTransitions.Count);

        foreach (WorkflowTransition<TState> transition in allTransitions)
        {
            if (transition.RequiredPermission is null)
            {
                allowed.Add(transition);
                continue;
            }

            bool hasPermission = await _permissionChecker.IsGrantedAsync(
                transition.RequiredPermission, cancellationToken).ConfigureAwait(false);

            if (hasPermission || transition.RequiresApproval)
            {
                // Include if user has permission OR if approval routing is available
                allowed.Add(transition);
            }
        }

        return allowed;
    }

    /// <inheritdoc/>
    public async Task<TransitionResult<TState>> TransitionAsync(
        TState currentState,
        TState targetState,
        TransitionContext? context = null,
        CancellationToken cancellationToken = default)
    {
        long startTimestamp = Stopwatch.GetTimestamp();
        using Activity? activity = WorkflowActivitySource.Source.StartActivity(WorkflowActivitySource.Transition);

        string fromState = currentState.ToString();
        string toState = targetState.ToString();

        activity?.SetTag("workflow.from_state", fromState);
        activity?.SetTag("workflow.to_state", toState);

        // Find the transition definition
        IReadOnlyList<WorkflowTransition<TState>> transitions = _definition.GetAllowedTransitions(currentState);
        WorkflowTransition<TState>? transition = transitions
            .FirstOrDefault(t => EqualityComparer<TState>.Default.Equals(t.To, targetState));

        if (transition is null)
        {
            RecordMetrics(fromState, toState, TransitionOutcome.InvalidTransition, startTimestamp, activity);
            return new TransitionResult<TState>
            {
                Succeeded = false,
                ResultingState = currentState,
                Outcome = TransitionOutcome.InvalidTransition,
            };
        }

        // Set comment in AsyncLocal context for the interceptor.
        // The scope is disposed when the method returns, clearing the AsyncLocal.
        using IDisposable? commentScope = context?.Comment is not null
            ? WorkflowTransitionContext.SetComment(context.Comment)
            : null;

        // Check permission if required
        if (transition.RequiredPermission is not null)
        {
            bool hasPermission = await _permissionChecker.IsGrantedAsync(
                transition.RequiredPermission, cancellationToken).ConfigureAwait(false);

            if (!hasPermission)
            {
                if (transition.RequiresApproval)
                {
                    RecordMetrics(fromState, toState, TransitionOutcome.ApprovalRequested, startTimestamp, activity);

                    // Route to pending review — the caller is responsible for
                    // actually setting the entity state and publishing the domain event
                    return new TransitionResult<TState>
                    {
                        Succeeded = true,
                        ResultingState = FindPendingReviewState(),
                        Outcome = TransitionOutcome.ApprovalRequested,
                    };
                }

                RecordMetrics(fromState, toState, TransitionOutcome.Denied, startTimestamp, activity);
                return new TransitionResult<TState>
                {
                    Succeeded = false,
                    ResultingState = currentState,
                    Outcome = TransitionOutcome.Denied,
                };
            }
        }

        RecordMetrics(fromState, toState, TransitionOutcome.Completed, startTimestamp, activity);

        // Transition approved — the caller sets the entity state
        return new TransitionResult<TState>
        {
            Succeeded = true,
            ResultingState = targetState,
            Outcome = TransitionOutcome.Completed,
        };
    }

    private void RecordMetrics(
        string fromState,
        string toState,
        TransitionOutcome outcome,
        long startTimestamp,
        Activity? activity)
    {
        string outcomeTag = outcome.ToString();
        TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);

        activity?.SetTag("workflow.outcome", outcomeTag);

        string? tenantId = _currentTenant.IsAvailable ? _currentTenant.Id.ToString() : null;
        _metrics.RecordTransitionCompleted(tenantId, outcomeTag, fromState, toState);
        _metrics.RecordTransitionDuration(tenantId, outcomeTag, elapsed);
    }

    /// <summary>
    /// Finds the "PendingReview" equivalent state in the TState enum.
    /// Convention: looks for enum value named "PendingReview" or with value 1
    /// (matching <see cref="Domain.WorkflowLifecycleStatus.PendingReview"/>).
    /// Falls back to the current initial state if not found.
    /// </summary>
    private TState FindPendingReviewState()
    {
        // Try to find a state named "PendingReview"
        if (Enum.TryParse("PendingReview", out TState pendingReview))
        {
            return pendingReview;
        }

        // Fallback: look for states that are targets of transitions from initial state
        // that might represent a review stage
        return _definition.InitialState;
    }
}
