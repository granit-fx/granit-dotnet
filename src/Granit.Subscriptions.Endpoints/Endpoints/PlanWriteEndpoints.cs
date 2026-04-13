using Granit.Guids;
using Granit.Http.Idempotency.Attributes;
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

internal static class PlanWriteEndpoints
{
    internal static RouteGroupBuilder MapPlanWriteEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/plans", CreatePlanAsync)
            .WithName("CreatePlan")
            .WithSummary("Creates a new plan in Draft status.")
            .WithDescription("Creates a plan that can be configured with prices and features before publishing.")
            .WithMetadata(new IdempotentAttribute())
            .Produces<PlanResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization(SubscriptionsPermissions.Plans.Manage);

        group.MapPut("/plans/{id:guid}", UpdatePlanAsync)
            .WithName("UpdatePlan")
            .WithSummary("Updates a draft plan.")
            .WithDescription("Only Draft plans can be updated. Published and Archived plans are immutable.")
            .WithMetadata(new IdempotentAttribute())
            .Produces<PlanResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(SubscriptionsPermissions.Plans.Manage);

        group.MapPost("/plans/{id:guid}/publish", PublishPlanAsync)
            .WithName("PublishPlan")
            .WithSummary("Publishes a draft plan, making it available for purchase.")
            .WithDescription("Transitions the plan from Draft to Published. This action is irreversible.")
            .WithMetadata(new IdempotentAttribute())
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(SubscriptionsPermissions.Plans.Manage);

        group.MapPost("/plans/{id:guid}/archive", ArchivePlanAsync)
            .WithName("ArchivePlan")
            .WithSummary("Archives a published plan.")
            .WithDescription("Archived plans are no longer purchasable but remain usable by existing subscribers.")
            .WithMetadata(new IdempotentAttribute())
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(SubscriptionsPermissions.Plans.Manage);

        return group;
    }

    private static async Task<Results<Created<PlanResponse>, ValidationProblem>> CreatePlanAsync(
        PlanCreateRequest request,
        [FromServices] IPlanWriter planWriter,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PricingModel>(request.PricingModel, true, out PricingModel pricingModel))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["PricingModel"] = [$"Invalid pricing model: {request.PricingModel}"],
            });
        }

        if (!Enum.TryParse<BillingInterval>(request.DefaultInterval, true, out BillingInterval interval))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["DefaultInterval"] = [$"Invalid billing interval: {request.DefaultInterval}"],
            });
        }

        var plan = Plan.Create(
            guidGenerator.Create(),
            request.Name,
            request.Description,
            pricingModel,
            interval,
            request.TrialDays,
            request.SeatLimit);

        await planWriter.AddAsync(plan, cancellationToken).ConfigureAwait(false);

        return TypedResults.Created($"/plans/{plan.Id}", PlanResponse.FromEntity(plan));
    }

    private static async Task<Results<Ok<PlanResponse>, NotFound, ProblemHttpResult>> UpdatePlanAsync(
        Guid id,
        PlanUpdateRequest request,
        [FromServices] IPlanReader planReader,
        [FromServices] IPlanWriter planWriter,
        CancellationToken cancellationToken)
    {
        Plan? plan = await planReader
            .GetByIdAsync(PlanId.Create(id), cancellationToken).ConfigureAwait(false);

        if (plan is null)
        {
            return TypedResults.NotFound();
        }

        try
        {
            plan.Update(request.Name, request.Description, request.SortOrder);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }

        await planWriter.UpdateAsync(plan, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(PlanResponse.FromEntity(plan));
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> PublishPlanAsync(
        Guid id,
        [FromServices] IPlanReader planReader,
        [FromServices] IPlanWriter planWriter,
        CancellationToken cancellationToken)
    {
        Plan? plan = await planReader
            .GetByIdAsync(PlanId.Create(id), cancellationToken).ConfigureAwait(false);

        if (plan is null)
        {
            return TypedResults.NotFound();
        }

        try
        {
            plan.Publish();
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }

        await planWriter.UpdateAsync(plan, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> ArchivePlanAsync(
        Guid id,
        [FromServices] IPlanReader planReader,
        [FromServices] IPlanWriter planWriter,
        CancellationToken cancellationToken)
    {
        Plan? plan = await planReader
            .GetByIdAsync(PlanId.Create(id), cancellationToken).ConfigureAwait(false);

        if (plan is null)
        {
            return TypedResults.NotFound();
        }

        try
        {
            plan.Archive();
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }

        await planWriter.UpdateAsync(plan, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }
}
