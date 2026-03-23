using System.Security.Claims;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Webhooks.Endpoints.Endpoints;

internal static class WebhookSubscriptionLifecycleEndpoints
{
    internal static RouteGroupBuilder MapLifecycleEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/subscriptions/{id:guid}/activate", Activate)
            .WithName("ActivateWebhookSubscription")
            .WithSummary("Activates a suspended webhook subscription.")
            .WithDescription(
                "Transitions a subscription from Suspended to Active status. "
                + "Deliveries will resume for matching events. "
                + "The consecutive failure counter is reset to zero.")
            .Produces<WebhookSubscriptionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/subscriptions/{id:guid}/suspend", Suspend)
            .WithName("SuspendWebhookSubscription")
            .WithSummary("Suspends an active webhook subscription.")
            .WithDescription(
                "Temporarily pauses event delivery for the subscription. "
                + "The caller's identity is recorded alongside the suspension reason. "
                + "Use the activate endpoint to resume deliveries.")
            .Produces<WebhookSubscriptionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/subscriptions/{id:guid}/deactivate", Deactivate)
            .WithName("DeactivateWebhookSubscription")
            .WithSummary("Permanently deactivates a webhook subscription.")
            .WithDescription(
                "Moves the subscription to the Deactivated terminal status. "
                + "No further deliveries will be attempted. A deactivation reason must be provided. "
                + "This action cannot be reversed — create a new subscription instead.")
            .Produces<WebhookSubscriptionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Ok<WebhookSubscriptionResponse>> Activate(
        Guid id,
        [FromServices] IWebhookSubscriptionWriter writer,
        [FromServices] IWebhookSubscriptionReader reader,
        CancellationToken cancellationToken)
    {
        await writer.ActivateAsync(id, cancellationToken).ConfigureAwait(false);

        WebhookSubscription? subscription = await reader
            .FindByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(WebhookSubscriptionReadEndpoints.MapToResponse(subscription!));
    }

    private static async Task<Ok<WebhookSubscriptionResponse>> Suspend(
        Guid id,
        ClaimsPrincipal user,
        [FromServices] IWebhookSubscriptionWriter writer,
        [FromServices] IWebhookSubscriptionReader reader,
        CancellationToken cancellationToken)
    {
        string suspendedBy = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";

        await writer.SuspendAsync(id, suspendedBy, "Manual suspension via admin endpoint", cancellationToken)
            .ConfigureAwait(false);

        WebhookSubscription? subscription = await reader
            .FindByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(WebhookSubscriptionReadEndpoints.MapToResponse(subscription!));
    }

    private static async Task<Ok<WebhookSubscriptionResponse>> Deactivate(
        Guid id,
        WebhookSubscriptionDeactivateRequest request,
        [FromServices] IWebhookSubscriptionWriter writer,
        [FromServices] IWebhookSubscriptionReader reader,
        CancellationToken cancellationToken)
    {
        await writer.DeactivateAsync(id, request.Reason, cancellationToken).ConfigureAwait(false);

        WebhookSubscription? subscription = await reader
            .FindByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(WebhookSubscriptionReadEndpoints.MapToResponse(subscription!));
    }
}
