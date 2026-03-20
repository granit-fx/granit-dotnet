using Granit.Workflow.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Workflow.Endpoints.Endpoints;

/// <summary>
/// Generic endpoint handlers for querying available transitions and executing
/// workflow state transitions via <see cref="IWorkflowManager{TState}"/>.
/// </summary>
/// <typeparam name="TState">Enum type representing the workflow states.</typeparam>
internal static class WorkflowTransitionEndpoints<TState> where TState : struct, Enum
{
    /// <summary>
    /// Registers GET and POST transition endpoints onto the given route group.
    /// </summary>
    internal static RouteGroupBuilder MapTransitionEndpoints(RouteGroupBuilder group)
    {
        string stateName = typeof(TState).Name;

        group.MapGet("/transitions", GetAvailableTransitionsAsync)
            .WithName($"GetAvailable{stateName}Transitions")
            .WithSummary($"Returns the transitions available from the given state for the current user.")
            .WithDescription(
                $"Returns the list of transitions available from the specified current state " +
                $"for the current user, considering their permissions. Transitions where the user " +
                $"lacks the required permission but approval routing is enabled are included " +
                $"with RequiresApproval = true.");

        group.MapPost("/transitions", ExecuteTransitionAsync)
            .WithName($"Execute{stateName}Transition")
            .WithSummary("Evaluates a workflow transition with permission checking and approval routing.")
            .WithDescription(
                "Attempts to transition from the current state to the target state. " +
                "Returns the outcome: Completed (direct transition), ApprovalRequested " +
                "(routed to pending review), Denied (no permission and no approval path), " +
                "or InvalidTransition (no such transition defined). The caller is responsible " +
                "for persisting the resulting state on the entity.");

        return group;
    }

    private static async Task<Results<Ok<WorkflowStatusResponse>, ProblemHttpResult>> GetAvailableTransitionsAsync(
        [FromQuery] string currentState,
        [FromServices] IWorkflowManager<TState> workflowManager,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse(currentState, ignoreCase: true, out TState state))
        {
            return TypedResults.Problem(
                detail: $"Unknown state '{currentState}'. Valid states: {string.Join(", ", Enum.GetNames<TState>())}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        IReadOnlyList<WorkflowTransition<TState>> transitions = await workflowManager
            .GetAllowedTransitionsAsync(state, cancellationToken)
            .ConfigureAwait(false);

        var response = new WorkflowStatusResponse(
            currentState,
            transitions.Select(t => new WorkflowTransitionResponse(
                t.To.ToString(),
                t.Name ?? t.To.ToString(),
                t.RequiredPermission is null || !t.RequiresApproval,
                t.RequiresApproval)).ToList());

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<WorkflowTransitionResultResponse>, ProblemHttpResult>> ExecuteTransitionAsync(
        [FromQuery] string currentState,
        WorkflowTransitionRequest request,
        [FromServices] IWorkflowManager<TState> workflowManager,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse(currentState, ignoreCase: true, out TState fromState))
        {
            return TypedResults.Problem(
                detail: $"Unknown current state '{currentState}'. Valid states: {string.Join(", ", Enum.GetNames<TState>())}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!Enum.TryParse(request.TargetState, ignoreCase: true, out TState toState))
        {
            return TypedResults.Problem(
                detail: $"Unknown target state '{request.TargetState}'. Valid states: {string.Join(", ", Enum.GetNames<TState>())}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        TransitionContext? context = request.Comment is not null
            ? new TransitionContext { Comment = request.Comment }
            : null;

        TransitionResult<TState> result = await workflowManager
            .TransitionAsync(fromState, toState, context, cancellationToken)
            .ConfigureAwait(false);

        var response = new WorkflowTransitionResultResponse(
            result.Succeeded,
            result.ResultingState.ToString(),
            result.Outcome.ToString());

        return TypedResults.Ok(response);
    }
}
