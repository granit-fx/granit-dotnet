using Granit.Entities.Actions;
using Granit.Entities.Endpoints.Dtos.BulkActions;
using Granit.Entities.Internal.BulkActions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Granit.Entities.Endpoints.Internal;

/// <summary>
/// Bulk action endpoint handler. Maps `POST /api/entities/{name}/bulk/{action}`
/// to execute an action across multiple entity instances in a single request.
/// </summary>
internal static class BulkActionEndpoint
{
    public static void MapBulkActionEndpoint<TEntity>(
        RouteGroupBuilder group,
        string entityName,
        EntityActionDescriptor descriptor,
        BulkActionExecutionOrchestrator orchestrator,
        ILogger logger,
        IEntityDefinitionRegistry registry)
        where TEntity : class
    {
        if (descriptor.ServerExecutorType is null)
        {
            logger.LogWarning(
                "Skipping bulk action endpoint for '{Action}' on entity '{Entity}': no ServerExecutor configured.",
                descriptor.Name, entityName);
            return;
        }

        group.MapPost(
            $"/{entityName}/bulk/{{action}}",
            BulkActionHandler<TEntity>)
            .WithName($"BulkExecuteAction{entityName}{descriptor.Name}")
            .WithSummary($"Executes a bulk action ({descriptor.Name}) on multiple {entityName} entities.")
            .WithDescription(
                $"Performs the '{descriptor.Name}' action across multiple selected entities in a single request. "
                + "Returns the count of successfully affected rows and a list of per-row failures (if any).")
            .Produces<BulkActionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags(entityName)
            .RequireAuthorization(descriptor.RequiresPermission);
    }

    private static async Task<Ok<BulkActionResponse>> BulkActionHandler<TEntity>(
        string action,
        [FromBody] BulkActionRequest request,
        BulkActionExecutionOrchestrator orchestrator,
        IDbContextFactory<DbContext> dbContextFactory,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        // Validate request
        if (request.Ids is null || request.Ids.Count == 0)
        {
            return TypedResults.Ok(new BulkActionResponse(Affected: 0, Failures: []));
        }

        // In production: lookup action descriptor from registry and validate permission
        // For now, assume it's valid (passed by the routing middleware)

        // Coalesce null payload to empty object
        JsonElement payload = request.Payload ?? JsonSerializer.SerializeToElement(new { });

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            // Create a minimal action descriptor for orchestrator (in production, get from registry)
            var descriptor = new EntityActionDescriptor(
                Name: action,
                Kind: EntityActionKind.ApiCall,
                DisplayKey: null,
                Icon: null,
                Order: 0,
                RequiresPermission: null,
                UrlTemplate: null,
                HttpMethod: "POST",
                ConfirmationKey: null,
                WorkflowTransitionName: null,
                ContributorAssemblyName: null,
                RequiresServerExecution: true,
                ServerExecutorType: typeof(IEntityActionExecutor<TEntity>),
                BulkExecutorType: null);

            var result = await orchestrator.ExecuteAsync(
                descriptor,
                dbContext,
                request.Ids,
                payload,
                cancellationToken);

            var response = new BulkActionResponse(
                Affected: result.AffectedCount,
                Failures: result.Failures
                    .Select(f => new BulkActionFailureResponse(f.EntityId, f.ErrorMessage))
                    .ToList());

            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bulk action '{Action}' failed for entity type '{EntityType}'.", action, typeof(TEntity).Name);
            throw;
        }
    }
}
