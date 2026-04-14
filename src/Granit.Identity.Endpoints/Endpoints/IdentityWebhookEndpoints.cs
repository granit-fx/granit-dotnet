using System.Text.Json;
using Granit.Identity.Endpoints.Dtos;
using Granit.Identity.Endpoints.Internal;
using Granit.Identity.Endpoints.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Endpoints.Endpoints;

/// <summary>
/// Webhook endpoint for receiving identity provider events.
/// Validates HMAC signature and calls <see cref="IUserLookupService"/> directly.
/// </summary>
/// <remarks>
/// For asynchronous processing via Wolverine, the application host can publish
/// <c>IdentityUserUpdatedEto</c> / <c>IdentityUserDeletedEto</c> messages instead
/// of using this endpoint.
/// </remarks>
internal static class IdentityWebhookEndpoints
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { PropertyNameCaseInsensitive = true };

    internal static IEndpointRouteBuilder MapWebhookEndpoint(
        this IEndpointRouteBuilder endpoints,
        string prefix)
    {
        string webhookRoute = string.IsNullOrEmpty(prefix)
            ? "identity/webhook"
            : $"{prefix.TrimEnd('/')}/identity/webhook";

        endpoints.MapPost(webhookRoute, HandleWebhookAsync)
            .WithName("IdentityWebhook")
            .WithSummary("Receives identity provider webhook events (user created/updated/deleted).")
            .WithDescription("Webhook receiver for identity provider event notifications. Validates the HMAC signature (if configured) and processes user_created, user_updated, and user_deleted events by refreshing or removing the corresponding cache entries. No authentication required — security relies on the HMAC signature validation.")
            .WithTags("Identity Webhook")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge);

        return endpoints;
    }

    /// <summary>Maximum webhook payload size (64 KB). Webhook payloads are small JSON objects.</summary>
    private const int MaxWebhookBodySize = 64 * 1024;

    private static async Task<Results<Ok, UnauthorizedHttpResult, ProblemHttpResult>> HandleWebhookAsync(
        HttpRequest request,
        WebhookSignatureValidator signatureValidator,
        [FromServices] IOptions<IdentityWebhookOptions> webhookOptions,
        [FromServices] IUserLookupService lookupService,
        CancellationToken cancellationToken)
    {
        // Read raw body for signature validation (server-verified size check)
        request.EnableBuffering();
        using var ms = new MemoryStream(capacity: 1024);
        await request.Body.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);

        if (ms.Length > MaxWebhookBodySize)
        {
            return TypedResults.Problem(
                detail: "Payload too large.",
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        byte[] body = ms.ToArray();
        request.Body.Position = 0;

        // Validate HMAC signature (fail-closed: rejects when secret is not configured)
        string? signature = request.Headers[webhookOptions.Value.SignatureHeaderName].FirstOrDefault();
        if (!signatureValidator.Validate(body, signature))
        {
            return TypedResults.Unauthorized();
        }

        // Parse payload
        IdentityWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<IdentityWebhookPayload>(body, s_jsonOptions);
        }
        catch (JsonException)
        {
            return TypedResults.Problem(
                detail: "Invalid JSON payload.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (payload is null || string.IsNullOrEmpty(payload.UserId) || string.IsNullOrEmpty(payload.EventType))
        {
            return TypedResults.Problem(
                detail: "Missing required fields: eventType, userId.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Process event directly via IUserLookupService
        switch (payload.EventType.ToLowerInvariant())
        {
            case "user_updated":
            case "user_created":
                await lookupService.RefreshByIdAsync(payload.UserId, cancellationToken).ConfigureAwait(false);
                break;

            case "user_deleted":
                await lookupService.DeleteByIdAsync(payload.UserId, cancellationToken).ConfigureAwait(false);
                break;

            default:
                return TypedResults.Problem(
                    detail: "Unsupported event type.",
                    statusCode: StatusCodes.Status400BadRequest);
        }

        return TypedResults.Ok();
    }
}
