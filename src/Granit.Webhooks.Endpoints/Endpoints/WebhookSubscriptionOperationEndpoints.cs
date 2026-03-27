using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Webhooks.Endpoints.Endpoints;

internal static class WebhookSubscriptionOperationEndpoints
{
    internal static RouteGroupBuilder MapOperationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/subscriptions/{id:guid}/rotate-secret", RotateSecret)
            .WithName("RotateWebhookSecret")
            .WithSummary("Rotates a subscription's signing secret.")
            .WithDescription(
                "Generates a new HMAC signing secret for the subscription. "
                + "The previous secret is invalidated immediately. "
                + "The new plain-text secret is returned once and cannot be retrieved later.")
            .Produces<WebhookSubscriptionRotateSecretResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/subscriptions/{id:guid}/test-ping", TestPing)
            .WithName("TestWebhookPing")
            .WithSummary("Sends a test ping to the subscription's target URL.")
            .WithDescription(
                "Dispatches a synthetic test event to the subscription endpoint and reports "
                + "the HTTP status code and round-trip duration. Does not affect delivery statistics "
                + "or the consecutive failure counter.")
            .Produces<WebhookSubscriptionTestPingResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/stats", GetStats)
            .WithName("GetWebhookStats")
            .WithSummary("Returns aggregate webhook delivery statistics.")
            .WithDescription(
                "Provides a summary of all webhook subscriptions: total count by status, "
                + "number of deliveries in the last 24 hours, success rate, and average response time.")
            .Produces<WebhookSubscriptionStatsResponse>();

        return group;
    }

    private static async Task<Results<Ok<WebhookSubscriptionRotateSecretResponse>, ProblemHttpResult>> RotateSecret(
        Guid id,
        [FromServices] IWebhookSubscriptionReader reader,
        [FromServices] IWebhookSubscriptionWriter writer,
        CancellationToken cancellationToken)
    {
        WebhookSubscription? subscription = await reader.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (subscription is null)
        {
            return TypedResults.Problem(detail: "Webhook subscription not found.", statusCode: StatusCodes.Status404NotFound);
        }

        string plainSecret = await writer.RotateSecretAsync(id, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(new WebhookSubscriptionRotateSecretResponse(plainSecret));
    }

    private static async Task<Results<Ok<WebhookSubscriptionTestPingResponse>, ProblemHttpResult>> TestPing(
        Guid id,
        [FromServices] IWebhookSubscriptionReader reader,
        [FromServices] IWebhookTestPingService testPingService,
        CancellationToken cancellationToken)
    {
        WebhookSubscription? subscription = await reader.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (subscription is null)
        {
            return TypedResults.Problem(detail: "Webhook subscription not found.", statusCode: StatusCodes.Status404NotFound);
        }

        WebhookTestPingResult result = await testPingService
            .SendTestPingAsync(id, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(new WebhookSubscriptionTestPingResponse(result.Success, result.HttpStatusCode, result.DurationMs));
    }

    private static async Task<Ok<WebhookSubscriptionStatsResponse>> GetStats(
        [FromServices] IWebhookStatsReader statsReader,
        CancellationToken cancellationToken)
    {
        WebhookStats stats = await statsReader.GetStatsAsync(cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(new WebhookSubscriptionStatsResponse(
            stats.TotalSubscriptions,
            stats.ActiveCount,
            stats.SuspendedCount,
            stats.DeactivatedCount,
            stats.DeliveriesLast24h,
            stats.SuccessRateLast24h,
            stats.AvgResponseTimeMsLast24h));
    }
}
