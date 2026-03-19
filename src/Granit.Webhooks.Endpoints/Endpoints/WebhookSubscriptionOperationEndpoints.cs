using Granit.Webhooks.Abstractions;
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
            .WithSummary("Rotate a subscription's signing secret");

        group.MapPost("/subscriptions/{id:guid}/test-ping", TestPing)
            .WithSummary("Send a test webhook to the subscription's target URL");

        group.MapGet("/stats", GetStats)
            .WithSummary("Get aggregate webhook statistics");

        return group;
    }

    private static async Task<Ok<WebhookSubscriptionRotateSecretResponse>> RotateSecret(
        Guid id,
        [FromServices] IWebhookSubscriptionWriter writer,
        CancellationToken cancellationToken)
    {
        string plainSecret = await writer.RotateSecretAsync(id, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(new WebhookSubscriptionRotateSecretResponse(plainSecret));
    }

    private static async Task<Ok<WebhookSubscriptionTestPingResponse>> TestPing(
        Guid id,
        [FromServices] IWebhookTestPingService testPingService,
        CancellationToken cancellationToken)
    {
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
