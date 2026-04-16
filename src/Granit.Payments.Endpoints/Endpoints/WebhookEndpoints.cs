using Granit.Payments.Commands;
using Granit.Payments.Contracts;
using Granit.Payments.Endpoints.Permissions;
using Granit.RateLimiting.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Granit.Payments.Endpoints.Endpoints;

internal static class WebhookEndpoints
{
    internal static RouteGroupBuilder MapWebhookEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/webhooks/{provider}", HandleWebhookAsync)
            .WithName("HandlePaymentWebhook")
            .WithSummary("Receives an inbound webhook from a payment provider.")
            .WithDescription(
                "Verifies the webhook signature, deduplicates the event using the processed event store, "
                + "and dispatches a ProcessWebhookCommand for asynchronous handling. "
                + "This endpoint is anonymous — authentication relies on the provider's signature verification.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AllowAnonymous()
            .RequireGranitRateLimiting(PaymentsRateLimitPolicies.Webhook);

        return group;
    }

    private static async Task<Results<Ok, ProblemHttpResult>> HandleWebhookAsync(
        string provider,
        HttpRequest httpRequest,
        [FromServices] IEnumerable<IPaymentWebhookVerifier> verifiers,
        [FromServices] IProcessedWebhookEventStore eventStore,
        [FromServices] IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        IPaymentWebhookVerifier? verifier = verifiers
            .FirstOrDefault(v => v.ProviderName.Equals(provider, StringComparison.OrdinalIgnoreCase));

        if (verifier is null)
        {
            return TypedResults.Problem(
                detail: $"Unknown payment provider '{provider}'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Reject oversized payloads (1 MB limit — webhook bodies are typically < 10 KB)
        const int maxBodySize = 1_048_576;
        if (httpRequest.ContentLength > maxBodySize)
        {
            return TypedResults.Problem(
                detail: "Request body too large.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        byte[] body;
        using (var ms = new MemoryStream())
        {
            await httpRequest.Body.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);

            if (ms.Length > maxBodySize)
            {
                return TypedResults.Problem(
                    detail: "Request body too large.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            body = ms.ToArray();
        }

        var headers = httpRequest.Headers
            .ToDictionary(h => h.Key, h => h.Value.ToString());

        PaymentWebhookVerificationResult result = await verifier
            .VerifyAsync(body, headers, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsValid)
        {
            return TypedResults.Problem(
                detail: result.RejectionReason ?? "Webhook signature verification failed.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        bool recorded = await eventStore
            .TryRecordAsync(provider, result.ProviderEventId ?? "", result.EventType ?? "", cancellationToken)
            .ConfigureAwait(false);

        if (!recorded)
        {
            return TypedResults.Problem(
                detail: "Duplicate webhook event.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var command = new ProcessWebhookCommand(
            provider,
            result.EventType ?? "",
            result.ProviderEventId ?? "",
            result.Payload);

        await messageBus.SendAsync(command).ConfigureAwait(false);

        return TypedResults.Ok();
    }
}
