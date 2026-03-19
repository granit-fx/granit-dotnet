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
            .WithSummary("Activate a suspended subscription");

        group.MapPost("/subscriptions/{id:guid}/suspend", Suspend)
            .WithSummary("Suspend an active subscription");

        group.MapPost("/subscriptions/{id:guid}/deactivate", Deactivate)
            .WithSummary("Permanently deactivate a subscription");

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
