using Granit.Http.Idempotency.Attributes;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Webhooks.Endpoints.Endpoints;

internal static class WebhookSubscriptionWriteEndpoints
{
    internal static RouteGroupBuilder MapWriteEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/subscriptions", Create)
            .WithName("CreateWebhookSubscription")
            .WithSummary("Creates a new webhook subscription.")
            .WithDescription(
                "Registers a new webhook subscription for the specified event type. "
                + "The response includes the plain-text signing secret which is only returned once. "
                + "The subscription starts in the Active status.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<WebhookSubscriptionCreatedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPut("/subscriptions/{id:guid}", Update)
            .WithName("UpdateWebhookSubscription")
            .WithSummary("Updates a webhook subscription's target URL.")
            .WithDescription(
                "Replaces the target URL of an existing subscription. "
                + "The subscription keeps its current status, secret, and event type. "
                + "Returns 404 if the subscription does not exist.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<WebhookSubscriptionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapDelete("/subscriptions/{id:guid}", Delete)
            .WithName("DeleteWebhookSubscription")
            .WithSummary("Deletes a webhook subscription.")
            .WithDescription(
                "Permanently removes a webhook subscription and all its associated delivery history. "
                + "This action is irreversible.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status204NoContent);

        return group;
    }

    private static async Task<Created<WebhookSubscriptionCreatedResponse>> Create(
        WebhookSubscriptionCreateRequest request,
        [FromServices] IWebhookSubscriptionWriter writer,
        CancellationToken cancellationToken)
    {
        WebhookSubscriptionCreatedResult result = await writer
            .CreateAsync(request.TargetUrl, request.EventType, null, cancellationToken)
            .ConfigureAwait(false);

        WebhookSubscription sub = result.Subscription;
        WebhookSubscriptionCreatedResponse response = new(
            sub.Id, sub.TargetUrl, sub.EventType, sub.Status, result.PlainSecret);

        return TypedResults.Created($"/subscriptions/{sub.Id}", response);
    }

    private static async Task<Results<Ok<WebhookSubscriptionResponse>, ProblemHttpResult>> Update(
        Guid id,
        WebhookSubscriptionUpdateRequest request,
        [FromServices] IWebhookSubscriptionWriter writer,
        [FromServices] IWebhookSubscriptionReader reader,
        CancellationToken cancellationToken)
    {
        await writer.UpdateTargetUrlAsync(id, request.TargetUrl, cancellationToken).ConfigureAwait(false);

        WebhookSubscription? subscription = await reader
            .FindByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            return TypedResults.Problem(detail: "Webhook subscription not found.", statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(WebhookSubscriptionReadEndpoints.MapToResponse(subscription));
    }

    private static async Task<NoContent> Delete(
        Guid id,
        [FromServices] IWebhookSubscriptionWriter writer,
        CancellationToken cancellationToken)
    {
        await writer.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }
}
