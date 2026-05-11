using Granit.Entities.Actions;
using Granit.Entities.Actions.Execution;
using Granit.Entities.Endpoints.Dtos.BulkActions;
using Granit.Entities.Internal.BulkActions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;
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
        ILogger logger)
        where TEntity : class
    {
        if (descriptor.ServerExecutorType is null)
        {
            logger.LogWarning(
                "Skipping bulk action endpoint for '{Action}' on entity '{Entity}': no ServerExecutor configured.",
                descriptor.Name, entityName);
            return;
        }

        RouteHandlerBuilder endpoint = group.MapPost(
            $"/{entityName}/bulk/{descriptor.Name}",
            ([FromBody] BulkActionRequest request,
             [FromServices] BulkActionExecutionOrchestrator endpointOrchestrator,
             [FromServices] IEntityDefinitionRegistry endpointRegistry,
             [FromServices] IDbContextFactory<DbContext> endpointDbContextFactory,
             [FromServices] ILoggerFactory endpointLoggerFactory,
             CancellationToken cancellationToken) =>
                BulkActionHandler<TEntity>(
                    request,
                    entityName,
                    descriptor.Name,
                    endpointOrchestrator,
                    endpointRegistry,
                    endpointDbContextFactory,
                    endpointLoggerFactory,
                    cancellationToken))
            .WithName($"BulkExecuteAction{entityName}{descriptor.Name}")
            .WithSummary($"Executes a bulk action ({descriptor.Name}) on multiple {entityName} entities.")
            .WithDescription(
                $"Performs the '{descriptor.Name}' action across multiple selected entities in a single request. "
                + "Returns the count of successfully affected rows and a list of per-row failures (if any).")
            .Produces<BulkActionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags(entityName);

        if (descriptor.RequiresPermission is { Length: > 0 } permission)
        {
            endpoint.RequireAuthorization(permission);
        }
        else
        {
            endpoint.RequireAuthorization();
        }
    }

    private static async Task<Results<Ok<BulkActionResponse>, ProblemHttpResult>> BulkActionHandler<TEntity>(
        [FromBody] BulkActionRequest request,
        string name,
        string action,
        [FromServices] BulkActionExecutionOrchestrator orchestrator,
        [FromServices] IEntityDefinitionRegistry registry,
        [FromServices] IDbContextFactory<DbContext> dbContextFactory,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        ILogger logger = loggerFactory.CreateLogger("Granit.Entities.BulkActionEndpoint");

        // Validate request
        if (request.Ids is null || request.Ids.Count == 0)
        {
            return TypedResults.Ok(new BulkActionResponse(Affected: 0, Failures: []));
        }

        IEntityDefinitionDescriptor? definitionRef = registry.GetByName(name);
        if (definitionRef is null)
        {
            return TypedResults.Problem(
                detail: $"No EntityDefinition is registered with name '{name}'.",
                statusCode: StatusCodes.Status404NotFound);
        }

        EntityActionDescriptor? descriptor = definitionRef.Descriptor.Actions
            .FirstOrDefault(a => string.Equals(a.Name, action, StringComparison.OrdinalIgnoreCase));

        if (descriptor is null || descriptor.ServerExecutorType is null || !descriptor.RequiresServerExecution)
        {
            return TypedResults.Problem(
                detail: $"Bulk action '{action}' is not configured for server-side execution on entity '{name}'.",
                statusCode: StatusCodes.Status404NotFound);
        }

        // Coalesce null payload to empty object
        JsonElement payload = request.Payload ?? JsonSerializer.SerializeToElement(new { });

        try
        {
            await using DbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            BulkActionResult result = await orchestrator.ExecuteAsync<TEntity>(
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
