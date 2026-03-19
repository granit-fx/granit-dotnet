using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Webhooks.Endpoints.Endpoints;

internal static class WebhookSubscriptionReadEndpoints
{
    internal static RouteGroupBuilder MapReadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/subscriptions/{id:guid}", GetById)
            .WithSummary("Get a webhook subscription by ID")
            .Produces<WebhookSubscriptionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Ok<WebhookSubscriptionResponse>, NotFound>> GetById(
        Guid id,
        [FromServices] IWebhookSubscriptionReader reader,
        CancellationToken cancellationToken)
    {
        WebhookSubscription? subscription = await reader
            .FindByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapToResponse(subscription));
    }

    internal static WebhookSubscriptionResponse MapToResponse(WebhookSubscription subscription) =>
        new(
            subscription.Id,
            subscription.TargetUrl,
            subscription.EventType,
            subscription.Status,
            subscription.ConsecutiveFailureCount,
            subscription.LastSuccessAt,
            subscription.CreatedAt,
            subscription.ModifiedAt);
}
