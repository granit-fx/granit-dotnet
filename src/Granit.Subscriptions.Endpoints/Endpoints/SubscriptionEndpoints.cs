using Granit.Authorization.Extensions;
using Granit.Guids;
using Granit.Http.Idempotency.Attributes;
using Granit.MultiTenancy;
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

internal static class SubscriptionEndpoints
{
    private const string TenantContextRequiredMessage = "Tenant context required.";

    internal static RouteGroupBuilder MapSubscriptionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/subscriptions/active", GetActiveSubscriptionAsync)
            .WithName("GetActiveSubscription")
            .WithSummary("Returns the active subscription for the current tenant.")
            .WithDescription("Returns the subscription in Active or Trial status. Returns 404 if no active subscription exists. Requires tenant context.")
            .Produces<SubscriptionResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(SubscriptionsPermissions.Subscriptions.Read)
            .AllowHostAccess();

        group.MapGet("/subscriptions/{id:guid}", GetSubscriptionByIdAsync)
            .WithName("GetSubscriptionById")
            .WithSummary("Returns a subscription by ID.")
            .WithDescription("Returns the full subscription details including seat count and dunning status.")
            .Produces<SubscriptionResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(SubscriptionsPermissions.Subscriptions.Read)
            .AllowHostAccess();

        group.MapPost("/subscriptions", CreateSubscriptionAsync)
            .WithName("CreateSubscription")
            .WithSummary("Creates a new subscription.")
            .WithDescription("Creates a subscription for the current tenant. If trialEndsAt is provided, starts in Trial status; otherwise starts as Active. Requires tenant context.")
            .WithMetadata(new IdempotentAttribute())
            .Produces<SubscriptionResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem()
            .RequireAuthorization(SubscriptionsPermissions.Subscriptions.Manage)
            .AllowHostAccess();

        group.MapPost("/subscriptions/{id:guid}/cancel", CancelSubscriptionAsync)
            .WithName("CancelSubscription")
            .WithSummary("Cancels a subscription.")
            .WithDescription("Cancels immediately or schedules cancellation at period end based on the request.")
            .WithMetadata(new IdempotentAttribute())
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(SubscriptionsPermissions.Subscriptions.Manage)
            .AllowHostAccess();

        group.MapPost("/subscriptions/{id:guid}/change-plan", ChangePlanAsync)
            .WithName("ChangeSubscriptionPlan")
            .WithSummary("Changes the subscription plan.")
            .WithDescription("Switches to a different plan. Only allowed for Active or Trial subscriptions.")
            .WithMetadata(new IdempotentAttribute())
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(SubscriptionsPermissions.Subscriptions.Manage)
            .AllowHostAccess();

        return group;
    }

    private static async Task<Results<Ok<SubscriptionResponse>, ProblemHttpResult>> GetActiveSubscriptionAsync(
        [FromServices] ISubscriptionReader reader,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.IsAvailable)
        {
            return TypedResults.Problem(TenantContextRequiredMessage, statusCode: StatusCodes.Status400BadRequest);
        }

        Subscription? sub = await reader
            .GetActiveForTenantAsync(currentTenant.Id!.Value, cancellationToken).ConfigureAwait(false);

        return sub is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound)
            : TypedResults.Ok(SubscriptionResponse.FromEntity(sub));
    }

    private static async Task<Results<Ok<SubscriptionResponse>, ProblemHttpResult>> GetSubscriptionByIdAsync(
        Guid id,
        [FromServices] ISubscriptionReader reader,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        Subscription? sub = await reader
            .GetByIdAsync(SubscriptionId.Create(id), cancellationToken).ConfigureAwait(false);

        if (sub is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        // Host context (AllowHostAccess): bypass tenant ownership check.
        // Tenant context: verify the subscription belongs to the current tenant.
        if (currentTenant.IsAvailable && sub.TenantId != currentTenant.Id!.Value)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(SubscriptionResponse.FromEntity(sub));
    }

    private static async Task<Results<Created<SubscriptionResponse>, ValidationProblem, ProblemHttpResult>> CreateSubscriptionAsync(
        SubscriptionCreateRequest request,
        [FromServices] ISubscriptionWriter writer,
        [FromServices] IPlanReader planReader,
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IClock clock,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.IsAvailable)
        {
            return TypedResults.Problem(TenantContextRequiredMessage, statusCode: StatusCodes.Status400BadRequest);
        }

        Plan? plan = await planReader
            .GetByIdAsync(PlanId.Create(request.PlanId), cancellationToken).ConfigureAwait(false);

        if (plan is null)
        {
            return TypedResults.Problem(
                detail: $"Plan '{request.PlanId}' not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        DateTimeOffset now = clock.Now;
        DateTimeOffset periodEnd = plan.DefaultInterval switch
        {
            BillingInterval.Monthly => now.AddMonths(1),
            BillingInterval.Quarterly => now.AddMonths(3),
            BillingInterval.Yearly => now.AddYears(1),
            _ => now.AddMonths(1),
        };

        // Pin to the current active price for the requested currency and default interval.
        PlanPrice? currentPrice = plan.GetCurrentPrice(request.Currency, plan.DefaultInterval);

        var sub = Subscription.Create(
            guidGenerator.Create(),
            currentTenant.Id!.Value,
            PlanId.Create(request.PlanId),
            request.Currency,
            new SubscriptionPeriod(now, periodEnd, BillingCycleAnchor: now),
            trialEndsAt: request.TrialEndsAt,
            planPriceId: currentPrice?.Id);

        await writer.AddAsync(sub, cancellationToken).ConfigureAwait(false);

        return TypedResults.Created(
            $"/subscriptions/{sub.Id}", SubscriptionResponse.FromEntity(sub));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> CancelSubscriptionAsync(
        Guid id,
        SubscriptionCancelRequest request,
        [FromServices] ISubscriptionReader reader,
        [FromServices] ISubscriptionWriter writer,
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] IClock clock,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.IsAvailable)
        {
            return TypedResults.Problem(TenantContextRequiredMessage, statusCode: StatusCodes.Status400BadRequest);
        }

        Subscription? sub = await reader
            .GetByIdAsync(SubscriptionId.Create(id), cancellationToken).ConfigureAwait(false);

        if (sub is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        if (sub.TenantId != currentTenant.Id!.Value)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        try
        {
            if (request.AtPeriodEnd)
            {
                sub.ScheduleCancelAtPeriodEnd();
            }
            else
            {
                sub.Cancel(request.Reason, clock.Now);
            }
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }

        await writer.UpdateAsync(sub, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ChangePlanAsync(
        Guid id,
        SubscriptionChangePlanRequest request,
        [FromServices] ISubscriptionReader reader,
        [FromServices] ISubscriptionWriter writer,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.IsAvailable)
        {
            return TypedResults.Problem(TenantContextRequiredMessage, statusCode: StatusCodes.Status400BadRequest);
        }

        Subscription? sub = await reader
            .GetByIdAsync(SubscriptionId.Create(id), cancellationToken).ConfigureAwait(false);

        if (sub is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        if (sub.TenantId != currentTenant.Id!.Value)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        try
        {
            sub.ChangePlan(PlanId.Create(request.NewPlanId));
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }

        await writer.UpdateAsync(sub, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }
}
