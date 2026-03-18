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
    public static IEndpointRouteBuilder MapGranitWebhooksRedelivery(
        this IEndpointRouteBuilder endpoints,
        string routePrefix = "webhooks")
    {
        endpoints
            .MapPost($"{routePrefix}/deliveries/{{deliveryId:guid}}/retry", HandleRetryAsync)
            .WithName("RetryWebhookDelivery")
            .WithTags("Webhooks")
            .WithSummary("Retries a previously failed webhook delivery attempt.")
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
