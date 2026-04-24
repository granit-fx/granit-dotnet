using Granit.Guids;
using Granit.Http.Idempotency.Attributes;
using Granit.Metering.Domain;
using Granit.Metering.Domain.ValueObjects;
using Granit.Metering.Dtos;
using Granit.Metering.Endpoints.Dtos;
using Granit.Metering.Endpoints.Permissions;
using Granit.Metering.Recompute;
using Granit.MultiTenancy;
using Granit.Workflow.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Metering.Endpoints.Endpoints;

internal static class UsageEndpoints
{
    internal static RouteGroupBuilder MapUsageEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/usage", GetUsageForPeriodAsync)
            .WithName("GetUsageForPeriod")
            .WithSummary("Returns aggregated usage for a meter over a specific period.")
            .WithDescription(
                "Queries pre-computed usage aggregates for the specified meter and time range. "
                + "The meterId, periodStart, and periodEnd query parameters are required. "
                + "Returns 404 if no aggregate exists for the given parameters.")
            .Produces<UsageAggregateResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(MeteringPermissions.Usage.Read);

        group.MapGet("/quota/{meterId:guid}", CheckQuotaAsync)
            .WithName("CheckMeteringQuota")
            .WithSummary("Returns the quota status for a specific meter.")
            .WithDescription(
                "Checks the current usage against the plan-defined quota for the specified meter. "
                + "Returns the current usage, limit, percentage used, and whether the quota is exceeded. "
                + "Meters without a defined quota are reported as unlimited.")
            .Produces<MeteringQuotaStatusResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization(MeteringPermissions.Usage.Read);

        group.MapPost("/events", RecordUsageEventsAsync)
            .WithName("RecordUsageEvents")
            .WithSummary("Records one or more usage events.")
            .WithDescription(
                "Accepts a batch of usage events and records them against their respective meter definitions. "
                + "Idempotency is enforced at two complementary layers: "
                + "(1) the HTTP `Idempotency-Key` header (required, RFC 8700) protects against network-level replay of the entire batch — "
                + "duplicate sends with the same header value return the cached response with `Idempotent-Replayed: true`, "
                + "and reuse with a different payload returns 422; "
                + "(2) per-event `IdempotencyKey` payload field is unique within a tenant — duplicate events across different batches are silently ignored "
                + "(allows partial-overlap retries to safely add only new events). "
                + "Requests without the `Idempotency-Key` header are rejected 422. "
                + "All events must have a positive quantity and a non-empty idempotency key.")
            .WithMetadata(new IdempotentAttribute())
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .RequireAuthorization(MeteringPermissions.Usage.Record);

        group.MapPost("/events/backfill", BackfillUsageEventsAsync)
            .WithName("BackfillUsageEvents")
            .WithSummary("Inserts historical usage events older than the standard 7-day ingestion window.")
            .WithDescription(
                "Accepts a batch of historical usage events with timestamps up to 365 days in the past "
                + "(future timestamps are still rejected). Events are deduplicated via the per-tenant unique "
                + "(TenantId, IdempotencyKey) index — duplicates are silently ignored. After insertion, the "
                + "service groups events by meter and triggers an automatic recompute over each meter's spanning "
                + "window so existing UsageAggregate rows immediately reflect the backfilled data. "
                + "Idempotent at the HTTP layer via the standard Idempotency-Key header.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<BackfillUsageResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .RequireAuthorization(MeteringPermissions.Events.Backfill);

        group.MapPost("/events/{id:guid}/deprecate", DeprecateEventAsync)
            .WithName("DeprecateMeterEvent")
            .WithSummary("Soft-deprecates a single meter event so it stops contributing to aggregates.")
            .WithDescription(
                "Marks the event as deprecated (audit-safe alternative to DELETE; ISO 27001 A.12.4 keeps the row "
                + "for the audit trail) and triggers an automatic recompute on the affected hourly bucket so the "
                + "UsageAggregate immediately reflects the change. "
                + "Returns 404 if the event does not exist; 409 if the event is already deprecated. "
                + "The original IdempotencyKey remains reserved by the unique index — re-ingestion is rejected, "
                + "preventing accidental resurrection of the deprecated event.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<DeprecateEventResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem()
            .RequireAuthorization(MeteringPermissions.Events.Manage);

        return group;
    }

    private static async Task<Results<Ok<UsageAggregateResponse>, ProblemHttpResult>> GetUsageForPeriodAsync(
        [FromQuery] Guid meterId,
        [FromQuery] DateTimeOffset periodStart,
        [FromQuery] DateTimeOffset periodEnd,
        [FromServices] IUsageReader usageReader,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.IsAvailable)
        {
            return TypedResults.Problem("Tenant context required.", statusCode: StatusCodes.Status400BadRequest);
        }

        UsageAggregate? aggregate = await usageReader
            .GetForPeriodAsync(
                currentTenant.Id!.Value,
                MeterDefinitionId.Create(meterId),
                periodStart,
                periodEnd,
                cancellationToken)
            .ConfigureAwait(false);

        return aggregate is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound)
            : TypedResults.Ok(UsageAggregateResponse.FromEntity(aggregate));
    }

    private static async Task<Results<Ok<MeteringQuotaStatusResponse>, ProblemHttpResult>> CheckQuotaAsync(
        Guid meterId,
        [FromServices] IQuotaChecker quotaChecker,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.IsAvailable)
        {
            return TypedResults.Problem("Tenant context required.", statusCode: StatusCodes.Status400BadRequest);
        }

        QuotaStatus status = await quotaChecker
            .CheckAsync(currentTenant.Id!.Value, MeterDefinitionId.Create(meterId), cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(MeteringQuotaStatusResponse.FromQuotaStatus(status));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RecordUsageEventsAsync(
        RecordUsageRequest request,
        [FromServices] IMeterEventRecorder recorder,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] IMeterDefinitionReader definitionReader,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.IsAvailable)
        {
            return TypedResults.Problem("Tenant context required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var meterIds = request.Events.Select(e => e.MeterDefinitionId).Distinct().ToList();
        foreach (Guid mid in meterIds)
        {
            MeterDefinition? def = await definitionReader
                .GetByIdAsync(MeterDefinitionId.Create(mid), cancellationToken)
                .ConfigureAwait(false);

            if (def is null)
            {
                return TypedResults.Problem(
                    detail: $"Meter definition '{mid}' not found.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            if (def.LifecycleStatus != WorkflowLifecycleStatus.Published)
            {
                return TypedResults.Problem(
                    detail: $"Meter definition '{mid}' is in '{def.LifecycleStatus}' status. "
                        + "Only Published meters accept ingestion.",
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }
        }

        var events = request.Events
            .Select(e => MeterEvent.Create(
                guidGenerator.Create(),
                e.MeterDefinitionId,
                e.IdempotencyKey,
                e.Quantity,
                e.Timestamp,
                e.Metadata))
            .ToList();

        await recorder.RecordBatchAsync(events, cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<BackfillUsageResponse>, ProblemHttpResult>> BackfillUsageEventsAsync(
        BackfillUsageRequest request,
        [FromServices] IUsageBackfillService backfillService,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] IMeterDefinitionReader definitionReader,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.IsAvailable)
        {
            return TypedResults.Problem("Tenant context required.", statusCode: StatusCodes.Status400BadRequest);
        }

        // Reject upfront if any referenced meter is missing or not Published. The
        // recompute pass would also reject Archived meters, but catching it here gives
        // a clean 422 with the offending id rather than a recompute error mid-batch.
        var meterIds = request.Events.Select(e => e.MeterDefinitionId).Distinct().ToList();
        foreach (Guid mid in meterIds)
        {
            MeterDefinition? def = await definitionReader
                .GetByIdAsync(MeterDefinitionId.Create(mid), cancellationToken)
                .ConfigureAwait(false);

            if (def is null)
            {
                return TypedResults.Problem(
                    detail: $"Meter definition '{mid}' not found.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            if (def.LifecycleStatus != WorkflowLifecycleStatus.Published)
            {
                return TypedResults.Problem(
                    detail: $"Meter definition '{mid}' is in '{def.LifecycleStatus}' status. "
                        + "Only Published meters accept backfill.",
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }
        }

        var events = request.Events
            .Select(e => MeterEvent.Create(
                guidGenerator.Create(),
                e.MeterDefinitionId,
                e.IdempotencyKey,
                e.Quantity,
                e.Timestamp,
                e.Metadata))
            .ToList();

        try
        {
            UsageBackfillResult result = await backfillService
                .BackfillAsync(events, cancellationToken)
                .ConfigureAwait(false);

            return TypedResults.Ok(new BackfillUsageResponse(
                result.EventsAccepted,
                result.MetersAffected,
                result.AggregatesRebuilt));
        }
        catch (UsageRecomputeRejectedException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                title: ex.ReasonCode,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static async Task<Results<Ok<DeprecateEventResponse>, ProblemHttpResult>> DeprecateEventAsync(
        Guid id,
        DeprecateEventRequest request,
        [FromServices] IMeterEventDeprecationService service,
        CancellationToken cancellationToken)
    {
        try
        {
            MeterEventDeprecationResult result = await service
                .DeprecateAsync(id, request.Reason, cancellationToken)
                .ConfigureAwait(false);

            return TypedResults.Ok(new DeprecateEventResponse(
                result.EventId,
                result.MeterDefinitionId,
                result.DeprecatedAt,
                result.Recompute.AggregatesRebuilt));
        }
        catch (MeterEventNotFoundException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound);
        }
        catch (InvalidOperationException ex)
        {
            // Raised by MeterEvent.Deprecate when the event is already deprecated.
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
    }
}
