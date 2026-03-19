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
            .WithSummary("Create a new webhook subscription");

        group.MapPut("/subscriptions/{id:guid}", Update)
            .WithSummary("Update a webhook subscription's target URL");

        group.MapDelete("/subscriptions/{id:guid}", Delete)
            .WithSummary("Delete a webhook subscription");

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

    private static async Task<Results<Ok<WebhookSubscriptionResponse>, NotFound>> Update(
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

        return TypedResults.Ok(WebhookSubscriptionReadEndpoints.MapToResponse(subscription!));
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
