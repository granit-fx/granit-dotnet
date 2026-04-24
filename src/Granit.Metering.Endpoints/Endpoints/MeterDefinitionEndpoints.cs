using Granit.Authorization.Extensions;
using Granit.Guids;
using Granit.Http.Idempotency.Attributes;
using Granit.Metering.Domain;
using Granit.Metering.Domain.ValueObjects;
using Granit.Metering.Endpoints.Dtos;
using Granit.Metering.Endpoints.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Metering.Endpoints.Endpoints;

internal static class MeterDefinitionEndpoints
{
    internal static RouteGroupBuilder MapMeterDefinitionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/meters", ListActiveMetersAsync)
            .WithName("ListActiveMeters")
            .WithSummary("Returns all active meter definitions for the current tenant.")
            .WithDescription(
                "Fetches the list of meter definitions that are currently active and accepting events. "
                + "Inactive meters are excluded from the results. "
                + "Use the GET by ID endpoint to retrieve a specific meter regardless of status.")
            .Produces<IReadOnlyList<MeterDefinitionResponse>>()
            .RequireAuthorization(MeteringPermissions.Meters.Read)
            .AllowHostAccess();

        group.MapGet("/meters/{id:guid}", GetMeterByIdAsync)
            .WithName("GetMeterDefinition")
            .WithSummary("Returns a meter definition by its unique identifier.")
            .WithDescription(
                "Fetches the full metadata of a meter definition including its aggregation type, unit, "
                + "and active status. Returns 404 if the meter does not exist.")
            .Produces<MeterDefinitionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(MeteringPermissions.Meters.Read)
            .AllowHostAccess();

        group.MapPost("/meters", CreateMeterAsync)
            .WithName("CreateMeterDefinition")
            .WithSummary("Creates a new meter definition.")
            .WithDescription(
                "Creates a meter definition with the specified name, unit, and aggregation type. "
                + "The meter is created in an active state and immediately accepts events. "
                + "Names must be unique within the tenant scope.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<MeterDefinitionResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization(MeteringPermissions.Meters.Manage);

        group.MapPut("/meters/{id:guid}", UpdateMeterAsync)
            .WithName("UpdateMeterDefinition")
            .WithSummary("Updates a meter definition.")
            .WithDescription(
                "Updates the name, unit, and description of an existing meter definition. "
                + "The aggregation type cannot be changed after creation to preserve data consistency. "
                + "Returns 404 if the meter does not exist.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<MeterDefinitionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .RequireAuthorization(MeteringPermissions.Meters.Manage);

        group.MapPost("/meters/{id:guid}/deactivate", DeactivateMeterAsync)
            .WithName("DeactivateMeterDefinition")
            .WithSummary("Deactivates a meter definition.")
            .WithDescription(
                "Marks the meter as inactive so it no longer accepts new events. "
                + "Existing usage data and aggregates are preserved. "
                + "Returns 404 if the meter does not exist.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(MeteringPermissions.Meters.Manage);

        return group;
    }

    private static async Task<Ok<IReadOnlyList<MeterDefinitionResponse>>> ListActiveMetersAsync(
        [FromServices] IMeterDefinitionReader reader,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<MeterDefinition> meters = await reader
            .GetActiveAsync(cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok<IReadOnlyList<MeterDefinitionResponse>>(
            meters.Select(MeterDefinitionResponse.FromEntity).ToList());
    }

    private static async Task<Results<Ok<MeterDefinitionResponse>, ProblemHttpResult>> GetMeterByIdAsync(
        Guid id,
        [FromServices] IMeterDefinitionReader reader,
        CancellationToken cancellationToken)
    {
        MeterDefinition? definition = await reader
            .GetByIdAsync(MeterDefinitionId.Create(id), cancellationToken).ConfigureAwait(false);

        return definition is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound)
            : TypedResults.Ok(MeterDefinitionResponse.FromEntity(definition));
    }

    private static async Task<Created<MeterDefinitionResponse>> CreateMeterAsync(
        MeterDefinitionCreateRequest request,
        [FromServices] IMeterDefinitionWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        var definition = MeterDefinition.Create(
            guidGenerator.Create(),
            request.Name,
            request.Unit,
            request.AggregationType,
            request.Description,
            request.ProductId);

        await writer.AddAsync(definition, cancellationToken).ConfigureAwait(false);

        return TypedResults.Created(
            $"/meters/{definition.Id}",
            MeterDefinitionResponse.FromEntity(definition));
    }

    private static async Task<Results<Ok<MeterDefinitionResponse>, ProblemHttpResult>> UpdateMeterAsync(
        Guid id,
        MeterDefinitionUpdateRequest request,
        [FromServices] IMeterDefinitionReader reader,
        [FromServices] IMeterDefinitionWriter writer,
        CancellationToken cancellationToken)
    {
        MeterDefinition? definition = await reader
            .GetByIdAsync(MeterDefinitionId.Create(id), cancellationToken).ConfigureAwait(false);

        if (definition is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        definition.Update(request.Name, request.Unit, request.Description);
        await writer.UpdateAsync(definition, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(MeterDefinitionResponse.FromEntity(definition));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeactivateMeterAsync(
        Guid id,
        [FromServices] IMeterDefinitionReader reader,
        [FromServices] IMeterDefinitionWriter writer,
        CancellationToken cancellationToken)
    {
        MeterDefinition? definition = await reader
            .GetByIdAsync(MeterDefinitionId.Create(id), cancellationToken).ConfigureAwait(false);

        if (definition is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        definition.Deactivate();
        await writer.UpdateAsync(definition, cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}
