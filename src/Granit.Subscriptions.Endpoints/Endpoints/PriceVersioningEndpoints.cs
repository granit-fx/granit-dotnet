using Granit.Guids;
using Granit.Http.Idempotency.Attributes;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Granit.Subscriptions.Endpoints.Dtos;
using Granit.Subscriptions.Endpoints.Permissions;
using Granit.Timing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Subscriptions.Endpoints.Endpoints;

internal static class PriceVersioningEndpoints
{
    internal static RouteGroupBuilder MapPriceVersioningEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/plans/{planId:guid}/prices", CreatePriceVersionAsync)
            .WithName("CreatePriceVersion")
            .WithSummary("Creates a new price version for a plan.")
            .WithDescription(
                "Adds a new price for the given currency and interval. If an active price already " +
                "exists for the same slot, it is marked as replaced. Allowed on Draft and Published " +
                "plans. Existing subscribers keep their pinned price (grandfathering).")
            .WithMetadata(new IdempotentAttribute())
            .Produces<PlanPriceResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem()
            .RequireAuthorization(SubscriptionsPermissions.Prices.Manage);

        group.MapGet("/plans/{planId:guid}/prices/history", GetPriceHistoryAsync)
            .WithName("GetPlanPriceHistory")
            .WithSummary("Returns the price version history for a plan.")
            .WithDescription(
                "Returns all price versions for the specified currency and interval, " +
                "ordered from newest to oldest. Includes replaced prices for audit trail.")
            .Produces<IReadOnlyList<PlanPriceResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(SubscriptionsPermissions.Prices.Read);

        group.MapPost("/subscriptions/{id:guid}/migrate-price", MigratePriceAsync)
            .WithName("MigrateSubscriptionPrice")
            .WithSummary("Migrates a subscription to a new price version.")
            .WithDescription(
                "Updates the pinned price for a specific subscription. Only allowed for " +
                "Active or Trial subscriptions. Publishes a SubscriptionPriceMigratedEto event.")
            .WithMetadata(new IdempotentAttribute())
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(SubscriptionsPermissions.Prices.Manage);

        group.MapPost("/subscriptions/bulk-migrate-price", BulkMigratePriceAsync)
            .WithName("BulkMigrateSubscriptionPrice")
            .WithSummary("Bulk-migrates subscriptions to a new price version.")
            .WithDescription(
                "Migrates all active subscriptions on a given plan to a new price version. " +
                "Optionally filters by old price ID. Returns the number of migrated subscriptions.")
            .WithMetadata(new IdempotentAttribute())
            .Produces<BulkMigratePriceResponse>()
            .ProducesValidationProblem()
            .RequireAuthorization(SubscriptionsPermissions.Prices.Manage);

        return group;
    }

    private static async Task<Results<Created<PlanPriceResponse>, NotFound, ProblemHttpResult, ValidationProblem>>
        CreatePriceVersionAsync(
            Guid planId,
            CreatePriceVersionRequest request,
            [FromServices] IPlanReader planReader,
            [FromServices] IPlanWriter planWriter,
            [FromServices] IGuidGenerator guidGenerator,
            [FromServices] IClock clock,
            CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<BillingInterval>(request.Interval, true, out BillingInterval interval))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Interval"] = [$"Invalid billing interval: {request.Interval}"],
            });
        }

        Plan? plan = await planReader
            .GetByIdAsync(PlanId.Create(planId), cancellationToken).ConfigureAwait(false);

        if (plan is null)
        {
            return TypedResults.NotFound();
        }

        DateTimeOffset now = clock.Now;

        var newPrice = PlanPrice.Create(
            guidGenerator.Create(),
            request.Amount,
            request.Currency.ToUpperInvariant(),
            interval,
            now);

        try
        {
            plan.AddPriceVersion(newPrice, now);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }

        await planWriter.UpdateAsync(plan, cancellationToken).ConfigureAwait(false);

        return TypedResults.Created(
            $"/plans/{planId}/prices/{newPrice.Id}",
            PlanPriceResponse.FromEntity(newPrice));
    }

    private static async Task<Results<Ok<IReadOnlyList<PlanPriceResponse>>, NotFound, ValidationProblem>>
        GetPriceHistoryAsync(
            Guid planId,
            [FromQuery] string currency,
            [FromQuery] string interval,
            [FromServices] IPlanReader planReader,
            CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<BillingInterval>(interval, true, out BillingInterval billingInterval))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["interval"] = [$"Invalid billing interval: {interval}"],
            });
        }

        Plan? plan = await planReader
            .GetByIdAsync(PlanId.Create(planId), cancellationToken).ConfigureAwait(false);

        if (plan is null)
        {
            return TypedResults.NotFound();
        }

        IReadOnlyList<PlanPrice> history = plan.GetPriceHistory(currency, billingInterval);

        return TypedResults.Ok<IReadOnlyList<PlanPriceResponse>>(
            history.Select(PlanPriceResponse.FromEntity).ToList());
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> MigratePriceAsync(
        Guid id,
        MigratePriceRequest request,
        [FromServices] ISubscriptionReader reader,
        [FromServices] ISubscriptionWriter writer,
        CancellationToken cancellationToken)
    {
        Subscription? sub = await reader
            .GetByIdAsync(SubscriptionId.Create(id), cancellationToken).ConfigureAwait(false);

        if (sub is null)
        {
            return TypedResults.NotFound();
        }

        try
        {
            bool changed = sub.MigratePrice(request.NewPlanPriceId);
            if (!changed)
            {
                return TypedResults.NoContent();
            }
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }

        await writer.UpdateAsync(sub, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<BulkMigratePriceResponse>, ValidationProblem>>
        BulkMigratePriceAsync(
            BulkMigratePriceRequest request,
            [FromServices] ISubscriptionReader reader,
            [FromServices] ISubscriptionWriter writer,
            CancellationToken cancellationToken)
    {
        IReadOnlyList<Subscription> subscriptions = await reader
            .GetActiveByPlanAsync(PlanId.Create(request.PlanId), cancellationToken)
            .ConfigureAwait(false);

        int migrated = 0;

        foreach (Subscription sub in subscriptions)
        {
            if (request.OldPlanPriceId.HasValue && sub.PlanPriceId != request.OldPlanPriceId)
            {
                continue;
            }

            if (sub.MigratePrice(request.NewPlanPriceId))
            {
                await writer.UpdateAsync(sub, cancellationToken).ConfigureAwait(false);
                migrated++;
            }
        }

        return TypedResults.Ok(new BulkMigratePriceResponse(migrated));
    }
}

/// <summary>Response for bulk price migration.</summary>
public sealed record BulkMigratePriceResponse(int MigratedCount);
