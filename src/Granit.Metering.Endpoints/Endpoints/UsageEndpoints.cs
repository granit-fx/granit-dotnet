using Granit.Guids;
using Granit.Http.Idempotency.Attributes;
using Granit.Metering.Domain;
using Granit.Metering.Domain.ValueObjects;
using Granit.Metering.Dtos;
using Granit.Metering.Endpoints.Dtos;
using Granit.Metering.Endpoints.Permissions;
using Granit.MultiTenancy;
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
            .RequireAuthorization(MeteringPermissions.Usage.Read);

        group.MapPost("/events", RecordUsageEventsAsync)
            .WithName("RecordUsageEvents")
            .WithSummary("Records one or more usage events.")
            .WithDescription(
                "Accepts a batch of usage events and records them against their respective meter definitions. "
                + "Duplicate events (same idempotency key within a tenant) are silently ignored. "
                + "All events must have a positive quantity and a non-empty idempotency key.")
            .WithMetadata(new IdempotentAttribute())
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .RequireAuthorization(MeteringPermissions.Usage.Record);

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

    private static async Task<Ok<MeteringQuotaStatusResponse>> CheckQuotaAsync(
        Guid meterId,
        [FromServices] IQuotaChecker quotaChecker,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        QuotaStatus status = await quotaChecker
            .CheckAsync(currentTenant.Id!.Value, MeterDefinitionId.Create(meterId), cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(MeteringQuotaStatusResponse.FromQuotaStatus(status));
    }

    private static async Task<NoContent> RecordUsageEventsAsync(
        RecordUsageRequest request,
        [FromServices] IMeterEventRecorder recorder,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
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
}
