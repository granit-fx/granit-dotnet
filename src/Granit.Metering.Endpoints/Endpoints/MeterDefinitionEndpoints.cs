using Granit.Authorization.Extensions;
using Granit.Guids;
using Granit.Http.Idempotency.Attributes;
using Granit.Metering.Domain;
using Granit.Metering.Domain.ValueObjects;
using Granit.Metering.Endpoints.Dtos;
using Granit.Metering.Endpoints.Permissions;
using Granit.Metering.Recompute;
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
        group.MapGet("/meters/{id:guid}", GetMeterByIdAsync)
            .WithName("GetMeterDefinition")
            .WithSummary("Returns a meter definition by its unique identifier.")
            .WithDescription(
                "Fetches the full metadata of a meter definition including its aggregation type, unit, "
                + "and lifecycle status. Returns 404 if the meter does not exist.")
            .Produces<MeterDefinitionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(MeteringPermissions.Meters.Read)
            .AllowHostAccess();

        group.MapPost("/meters", CreateMeterAsync)
            .WithName("CreateMeterDefinition")
            .WithSummary("Creates a new meter definition.")
            .WithDescription(
                "Creates a meter definition with the specified name, unit, and aggregation type. "
                + "The meter starts in Draft status; it must be Published before it accepts events. "
                + "Names must be unique within the tenant scope.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<MeterDefinitionResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization(MeteringPermissions.Meters.Manage);

        group.MapPut("/meters/{id:guid}", UpdateMeterAsync)
            .WithName("UpdateMeterDefinition")
            .WithSummary("Updates a Draft meter definition.")
            .WithDescription(
                "Updates the name, unit, and description of a meter definition in Draft status. "
                + "The aggregation type cannot be changed after creation to preserve data consistency. "
                + "Returns 404 if the meter does not exist, or 409 if the meter is not in Draft status.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<MeterDefinitionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem()
            .RequireAuthorization(MeteringPermissions.Meters.Manage);

        group.MapPost("/meters/{id:guid}/publish", PublishMeterAsync)
            .WithName("PublishMeterDefinition")
            .WithSummary("Publishes a Draft meter definition.")
            .WithDescription(
                "Transitions a Draft meter to Published status. Only Published meters accept ingestion. "
                + "Returns 404 if the meter does not exist, or 409 if the meter is not in Draft status.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(MeteringPermissions.Meters.Manage);

        group.MapPost("/meters/{id:guid}/archive", ArchiveMeterAsync)
            .WithName("ArchiveMeterDefinition")
            .WithSummary("Archives a Published meter definition.")
            .WithDescription(
                "Transitions a Published meter to Archived status. Existing aggregates and history are preserved; "
                + "ingestion of new events is rejected. "
                + "Returns 404 if the meter does not exist, or 409 if the meter is not in Published status.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(MeteringPermissions.Meters.Manage);

        group.MapPost("/meters/{id:guid}/recompute", RecomputeMeterUsageAsync)
            .WithName("RecomputeMeterUsage")
            .WithSummary("Recomputes UsageAggregate rows for a meter over a chosen window.")
            .WithDescription(
                "Re-runs the aggregation pipeline over the requested [from, to) window, "
                + "rebuilding hourly UsageAggregate rows from the raw MeterEvent data. "
                + "Window edges are snapped to hourly buckets server-side. The global ingestion "
                + "watermark is never rewound — concurrent ingestion past the upper bound is unaffected. "
                + "Mutually-excludes with the hourly aggregation job on the same (meter, tenant) via a "
                + "transaction-scoped database lock. Returns 422 when the window is empty/inverted/in the "
                + "future, when the meter is unknown, or when the meter is Archived.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<RecomputeUsageResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesValidationProblem()
            .RequireAuthorization(MeteringPermissions.Meters.Manage);

        group.MapPost("/meters/{id:guid}/deactivate", DeactivateMeterAsync)
            .WithName("DeactivateMeterDefinition")
            .WithSummary("[DEPRECATED] Alias for /meters/{id}/archive — will be removed in next major release.")
            .WithDescription(
                "Deprecated alias for the Archive endpoint, kept for one release to ease migration. "
                + "Same semantics: transitions a Published meter to Archived. The response carries "
                + "the standard `Deprecation: true` and `Sunset` headers (RFC 8594). New callers MUST "
                + "use `POST /meters/{id}/archive` instead.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(MeteringPermissions.Meters.Manage);

        return group;
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
            request.ProductId,
            request.DistinctProperty);

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

        try
        {
            definition.Update(request.Name, request.Unit, request.Description);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }

        await writer.UpdateAsync(definition, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(MeterDefinitionResponse.FromEntity(definition));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> PublishMeterAsync(
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

        try
        {
            definition.Publish();
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }

        await writer.UpdateAsync(definition, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ArchiveMeterAsync(
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

        try
        {
            definition.Archive();
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }

        await writer.UpdateAsync(definition, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeactivateMeterAsync(
        Guid id,
        HttpContext httpContext,
        [FromServices] IMeterDefinitionReader reader,
        [FromServices] IMeterDefinitionWriter writer,
        CancellationToken cancellationToken)
    {
        // RFC 8594 deprecation signal — kept for one release as alias for /archive.
        httpContext.Response.Headers["Deprecation"] = "true";
        httpContext.Response.Headers["Sunset"] = "Wed, 31 Dec 2026 23:59:59 GMT";
        httpContext.Response.Headers["Link"] = "</metering/meters/{id}/archive>; rel=\"successor-version\"";

        return await ArchiveMeterAsync(id, reader, writer, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Results<Ok<RecomputeUsageResponse>, ProblemHttpResult>> RecomputeMeterUsageAsync(
        Guid id,
        RecomputeUsageRequest request,
        [FromServices] IUsageRecomputeService service,
        CancellationToken cancellationToken)
    {
        try
        {
            UsageRecomputeResult result = await service
                .RecomputeAsync(
                    new UsageRecomputeRequest(id, request.From, request.To),
                    cancellationToken)
                .ConfigureAwait(false);

            return TypedResults.Ok(new RecomputeUsageResponse(
                result.MeterDefinitionId,
                result.WindowStart,
                result.WindowEnd,
                result.EventsScanned,
                result.AggregatesRebuilt,
                result.DurationMilliseconds));
        }
        catch (UsageRecomputeRejectedException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                title: ex.ReasonCode,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }
}
