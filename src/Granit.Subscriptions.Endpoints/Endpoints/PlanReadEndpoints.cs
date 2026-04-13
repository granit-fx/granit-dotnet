using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Granit.Subscriptions.Endpoints.Dtos;
using Granit.Subscriptions.Endpoints.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Subscriptions.Endpoints.Endpoints;

internal static class PlanReadEndpoints
{
    internal static RouteGroupBuilder MapPlanReadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/plans", ListPlansAsync)
            .WithName("ListPlans")
            .WithSummary("Returns all published plans.")
            .WithDescription("Returns plans available for purchase (Published status). Archived and Draft plans are excluded.")
            .Produces<IReadOnlyList<PlanResponse>>()
            .RequireAuthorization(SubscriptionsPermissions.Plans.Read);

        group.MapGet("/plans/{id:guid}", GetPlanByIdAsync)
            .WithName("GetPlanById")
            .WithSummary("Returns a plan by ID.")
            .WithDescription("Returns the full plan details including prices. Works for any lifecycle status (Draft, Published, Archived).")
            .Produces<PlanResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(SubscriptionsPermissions.Plans.Read);

        return group;
    }

    private static async Task<Ok<IReadOnlyList<PlanResponse>>> ListPlansAsync(
        [FromServices] IPlanReader planReader,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Plan> plans = await planReader
            .GetAvailablePlansAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<PlanResponse> response = plans
            .Select(PlanResponse.FromEntity).ToList();

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<PlanResponse>, ProblemHttpResult>> GetPlanByIdAsync(
        Guid id,
        [FromServices] IPlanReader planReader,
        CancellationToken cancellationToken)
    {
        Plan? plan = await planReader
            .GetByIdAsync(PlanId.Create(id), cancellationToken).ConfigureAwait(false);

        if (plan is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(PlanResponse.FromEntity(plan));
    }
}
