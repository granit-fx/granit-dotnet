using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Handlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Webhooks.Endpoints;

/// <summary>
/// Minimal API endpoint for manual redelivery of a failed webhook delivery attempt.
/// </summary>
public static class WebhookRedeliveryEndpoint
{
    /// <summary>
    /// Maps <c>POST /webhooks/deliveries/{deliveryId}/retry</c> (or custom prefix).
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="routePrefix">Route prefix. Default <c>"webhooks"</c>.</param>
    /// <param name="tagName">
    /// OpenAPI tag. Default <c>"Webhooks"</c> — matches <c>WebhooksEndpointsOptions.TagName</c>.
    /// Override when the host customizes the Webhooks tag.
    /// </param>
    public static IEndpointRouteBuilder MapGranitWebhooksRedelivery(
        this IEndpointRouteBuilder endpoints,
        string routePrefix = "webhooks",
        string tagName = "Webhooks")
    {
        endpoints
            .MapPost($"{routePrefix}/deliveries/{{deliveryId:guid}}/retry", HandleRetryAsync)
            .WithName("RetryWebhookDelivery")
            .WithTags(tagName)
            .WithSummary("Retries a previously failed webhook delivery attempt.")
            .WithDescription("Enqueues a manual redelivery for a previously failed webhook delivery attempt. Returns 404 if the delivery does not exist, 409 if the delivery is not in a retryable state (e.g., already succeeded or retry in progress), and 400 for other validation errors.")
            .RequireAuthorization()
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> HandleRetryAsync(
        Guid deliveryId,
        RetryWebhookHandler retryHandler,
        [FromServices] IWebhookCommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        RetryWebhookResult result = await retryHandler
            .HandleAsync(deliveryId, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            int statusCode = result.ErrorKind switch
            {
                RetryWebhookErrorKind.NotFound => StatusCodes.Status404NotFound,
                RetryWebhookErrorKind.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            };

            return TypedResults.Problem(detail: result.Error, statusCode: statusCode);
        }

        await dispatcher.DispatchAsync(result.Command!, cancellationToken).ConfigureAwait(false);

        return TypedResults.Accepted(string.Empty);
    }
}
