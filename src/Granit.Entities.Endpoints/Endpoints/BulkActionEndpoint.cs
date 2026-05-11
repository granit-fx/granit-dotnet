using System.Security.Claims;
using Granit.Authorization;
using Granit.Entities.Actions;
using Granit.Entities.Actions.Execution;
using Granit.Entities.Endpoints.Dtos;
using Granit.Entities.Endpoints.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Entities.Endpoints.Endpoints;

/// <summary>
/// <c>POST /api/entities/{name}/bulk/{action}</c> — synchronous bulk runner for
/// any entity action that opted into server-side execution via
/// <c>.ServerExecutor&lt;TExecutor&gt;()</c> (ADR-056, story #1822). One round-trip
/// in, one batched <see cref="Granit.Events.EntityBulkUpdatedEvent{TEntity}"/>
/// out, per-row failure isolation.
/// </summary>
internal static class BulkActionEndpoint
{
    /// <summary>
    /// Mounts the bulk-action route on the entity-endpoint group.
    /// </summary>
    public static RouteGroupBuilder MapBulkActionEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{name}/bulk/{action}", HandleAsync)
            .WithName("PostEntityBulkAction")
            .WithSummary("Executes a registered entity action against the supplied rows in one round-trip.")
            .WithDescription("Resolves the entity definition and the named action, gates the call through the action's RequiresPermission (defense in depth), then dispatches the run via the framework's BulkActionExecutionOrchestrator. Hosts wire a scoped IEntityActionExecutor<TEntity> per action; an optional IBulkActionExecutor<TEntity> shortcut lets the executor batch the work (one UPDATE … WHERE Id IN (…) call). Per-row failures are returned in the response payload; the call itself returns HTTP 200 whenever the bulk surface accepted the request. One EntityBulkUpdatedEvent<TEntity> is emitted via ILocalEventBus when at least one row succeeded and the entity implements IEmitEntityLifecycleEvents.")
            .Produces<BulkActionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Ok<BulkActionResponse>, ProblemHttpResult>> HandleAsync(
        string name,
        string action,
        BulkActionRequest request,
        [FromServices] IEntityDefinitionRegistry registry,
        [FromServices] BulkActionExecutionOrchestrator orchestrator,
        [FromServices] IPermissionChecker permissionChecker,
        HttpContext httpContext,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Resolve the entity definition.
        IEntityDefinitionDescriptor? definitionRef = registry.GetByName(name);
        if (definitionRef is null)
        {
            return TypedResults.Problem(
                detail: $"No EntityDefinition is registered with name '{name}'.",
                statusCode: StatusCodes.Status404NotFound);
        }

        EntityDefinitionDescriptor descriptor = definitionRef.Descriptor;

        // 2. Resolve the action by name.
        EntityActionDescriptor? actionDescriptor = descriptor.Actions
            .FirstOrDefault(a => string.Equals(a.Name, action, StringComparison.Ordinal));

        if (actionDescriptor is null)
        {
            return TypedResults.Problem(
                detail: $"Entity '{name}' has no action named '{action}'.",
                statusCode: StatusCodes.Status404NotFound);
        }

        // 3. Reject actions that did not opt into server-side execution. The
        //    selection-bar renderer still uses the per-row URL for those.
        if (!actionDescriptor.RequiresServerExecution || actionDescriptor.ServerExecutorType is null)
        {
            return TypedResults.Problem(
                detail: $"Action '{action}' on entity '{name}' is declarative — it does not opt into server-side execution via .ServerExecutor<>().",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // 4. Permission gate inherits from the action's RequiresPermission.
        //    Defense in depth: row-level visibility is the executor's job.
        if (actionDescriptor.RequiresPermission is { } permission)
        {
            bool granted = await permissionChecker
                .IsGrantedAsync(permission, cancellationToken)
                .ConfigureAwait(false);
            if (!granted)
            {
                return TypedResults.Problem(
                    detail: $"You do not have permission '{permission}' required by action '{action}'.",
                    statusCode: StatusCodes.Status403Forbidden);
            }
        }

        // 5. Dispatch the run. The orchestrator picks IBulkActionExecutor<T>
        //    when registered, otherwise loops the per-row executor.
        BulkActionDispatchResult result = await orchestrator
            .DispatchAsync(
                entityType: descriptor.EntityType,
                serverExecutorType: actionDescriptor.ServerExecutorType,
                ids: request.Ids,
                payload: request.Payload,
                scopedServices: httpContext.RequestServices,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(new BulkActionResponse(result.Affected, result.Failures));
    }
}
