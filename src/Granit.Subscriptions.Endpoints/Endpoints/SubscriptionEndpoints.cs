using Granit.Guids;
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
    internal static RouteGroupBuilder MapSubscriptionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/subscriptions", ListSubscriptionsAsync)
            .WithName("ListSubscriptions")
            .WithSummary("Returns all subscriptions for the current tenant.")
            .WithDescription("Returns subscriptions in any status. Use the 'active' endpoint for the current active subscription only.")
            .Produces<IReadOnlyList<SubscriptionResponse>>()
            .RequireAuthorization(SubscriptionsPermissions.Subscriptions.Read);

        group.MapGet("/subscriptions/active", GetActiveSubscriptionAsync)
            .WithName("GetActiveSubscription")
            .WithSummary("Returns the active subscription for the current tenant.")
            .WithDescription("Returns the subscription in Active or Trial status. Returns 404 if no active subscription exists.")
            .Produces<SubscriptionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(SubscriptionsPermissions.Subscriptions.Read);

        group.MapGet("/subscriptions/{id:guid}", GetSubscriptionByIdAsync)
            .WithName("GetSubscriptionById")
            .WithSummary("Returns a subscription by ID.")
            .WithDescription("Returns the full subscription details including seat count and dunning status.")
            .Produces<SubscriptionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(SubscriptionsPermissions.Subscriptions.Read);

        group.MapPost("/subscriptions", CreateSubscriptionAsync)
            .WithName("CreateSubscription")
            .WithSummary("Creates a new subscription.")
            .WithDescription("Creates a subscription for the current tenant. If trialEndsAt is provided, starts in Trial status; otherwise starts as Active.")
            .Produces<SubscriptionResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization(SubscriptionsPermissions.Subscriptions.Manage);

        group.MapPost("/subscriptions/{id:guid}/cancel", CancelSubscriptionAsync)
            .WithName("CancelSubscription")
            .WithSummary("Cancels a subscription.")
            .WithDescription("Cancels immediately or schedules cancellation at period end based on the request.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(SubscriptionsPermissions.Subscriptions.Manage);

        group.MapPost("/subscriptions/{id:guid}/change-plan", ChangePlanAsync)
            .WithName("ChangeSubscriptionPlan")
            .WithSummary("Changes the subscription plan.")
            .WithDescription("Switches to a different plan. Only allowed for Active or Trial subscriptions.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(SubscriptionsPermissions.Subscriptions.Manage);

        return group;
    }

    private static async Task<Ok<IReadOnlyList<SubscriptionResponse>>> ListSubscriptionsAsync(
        [FromServices] ISubscriptionReader reader,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Subscription> subs = await reader
            .GetByTenantAsync(currentTenant.Id!.Value, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok<IReadOnlyList<SubscriptionResponse>>(
            subs.Select(SubscriptionResponse.FromEntity).ToList());
    }

    private static async Task<Results<Ok<SubscriptionResponse>, NotFound>> GetActiveSubscriptionAsync(
        [FromServices] ISubscriptionReader reader,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        Subscription? sub = await reader
            .GetActiveForTenantAsync(currentTenant.Id!.Value, cancellationToken).ConfigureAwait(false);

        return sub is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(SubscriptionResponse.FromEntity(sub));
    }

    private static async Task<Results<Ok<SubscriptionResponse>, NotFound>> GetSubscriptionByIdAsync(
        Guid id,
        [FromServices] ISubscriptionReader reader,
        CancellationToken cancellationToken)
    {
        Subscription? sub = await reader
            .GetByIdAsync(SubscriptionId.Create(id), cancellationToken).ConfigureAwait(false);

        return sub is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(SubscriptionResponse.FromEntity(sub));
    }

    private static async Task<Results<Created<SubscriptionResponse>, ValidationProblem>> CreateSubscriptionAsync(
        SubscriptionCreateRequest request,
        [FromServices] ISubscriptionWriter writer,
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IClock clock,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.Now;
        var sub = Subscription.Create(
            guidGenerator.Create(),
            currentTenant.Id!.Value,
            PlanId.Create(request.PlanId),
            request.Currency,
            periodStart: now,
            periodEnd: now.AddMonths(1),
            billingCycleAnchor: now,
            trialEndsAt: request.TrialEndsAt);

        await writer.AddAsync(sub, cancellationToken).ConfigureAwait(false);

        return TypedResults.Created(
            $"/subscriptions/{sub.Id}", SubscriptionResponse.FromEntity(sub));
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> CancelSubscriptionAsync(
        Guid id,
        SubscriptionCancelRequest request,
        [FromServices] ISubscriptionReader reader,
        [FromServices] ISubscriptionWriter writer,
        [FromServices] IClock clock,
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

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> ChangePlanAsync(
        Guid id,
        SubscriptionChangePlanRequest request,
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
