using Granit.Exceptions;
using Granit.Http.Idempotency;
using Granit.Timing;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Webhooks.Endpoints.Endpoints;

/// <summary>
/// Admin endpoints managing the per-subscription <see cref="WebhookSigningKey"/> collection.
/// </summary>
/// <remarks>
/// All routes share the parent subscription's permissions:
/// <c>Webhooks.Subscriptions.Read</c> for listing, <c>Webhooks.Subscriptions.Manage</c> for
/// rotation and revocation. No dedicated permission family is introduced — keys are an
/// integral part of the subscription aggregate.
/// </remarks>
internal static class WebhookSigningKeyEndpoints
{
    /// <summary>
    /// Maps the read-side signing key endpoint (GET /subscriptions/{id}/keys).
    /// Requires <c>Webhooks.Subscriptions.Read</c> (inherited from the parent route group).
    /// </summary>
    internal static RouteGroupBuilder MapSigningKeyReadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/subscriptions/{id:guid}/keys", ListKeys)
            .WithName("ListWebhookSigningKeys")
            .WithSummary("Lists the signing keys associated with a subscription.")
            .WithDescription(
                "Returns all signing keys for the subscription, including Active, Retired (within "
                + "the rotation grace period), and Revoked entries. The protected secret value is "
                + "never disclosed; rotate the key to obtain a new plain-text secret.")
            .Produces<IReadOnlyList<WebhookSigningKeyResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    /// <summary>
    /// Maps the write-side signing key endpoints (POST and DELETE).
    /// Caller must apply <c>Webhooks.Subscriptions.Manage</c> via <c>RequireAuthorization</c>.
    /// </summary>
    internal static RouteGroupBuilder MapSigningKeyWriteEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/subscriptions/{id:guid}/keys", RotateKey)
            .WithName("RotateWebhookSigningKey")
            .WithSummary("Rotates the subscription's signing key with overlap semantics.")
            .WithDescription(
                "Generates a new Active signing key and moves the previous Active key to Retired "
                + "for a configurable grace period (default 24 hours, controlled by "
                + "WebhooksOptions.RetiredKeyGracePeriod). Verification accepts both keys until the "
                + "Retired key expires. The new plain-text secret is returned exactly once.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<WebhookSigningKeyCreatedResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/subscriptions/{id:guid}/keys/{keyId:guid}", RevokeKey)
            .WithName("RevokeWebhookSigningKey")
            .WithSummary("Revokes a specific signing key.")
            .WithDescription(
                "Marks the targeted key as Revoked. Verification will reject signatures produced "
                + "with this key from now on. The last Active key cannot be revoked — rotate first "
                + "to introduce a new Active key, then revoke the old one.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Ok<IReadOnlyList<WebhookSigningKeyResponse>>, ProblemHttpResult>> ListKeys(
        Guid id,
        [FromServices] IWebhookSubscriptionReader subscriptionReader,
        [FromServices] IWebhookSigningKeyReader keyReader,
        CancellationToken cancellationToken)
    {
        WebhookSubscription? subscription = await subscriptionReader
            .FindByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (subscription is null)
        {
            return TypedResults.Problem(detail: "Webhook subscription not found.", statusCode: StatusCodes.Status404NotFound);
        }

        IReadOnlyList<WebhookSigningKey> keys = await keyReader
            .GetForSubscriptionAsync(id, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<WebhookSigningKeyResponse> projection = [.. keys.Select(k => new WebhookSigningKeyResponse(
            k.Id,
            k.SubscriptionId,
            k.CreatedAt,
            k.ExpiresAt,
            k.RevokedAt,
            k.LastRotationNotificationAt,
            k.Status))];

        return TypedResults.Ok(projection);
    }

    private static async Task<Results<Created<WebhookSigningKeyCreatedResponse>, ProblemHttpResult>> RotateKey(
        Guid id,
        [FromServices] IWebhookSubscriptionReader subscriptionReader,
        [FromServices] IWebhookSigningKeyWriter keyWriter,
        [FromServices] IWebhookSigningKeyReader keyReader,
        [FromServices] IClock clock,
        CancellationToken cancellationToken)
    {
        WebhookSubscription? subscription = await subscriptionReader
            .FindByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (subscription is null)
        {
            return TypedResults.Problem(detail: "Webhook subscription not found.", statusCode: StatusCodes.Status404NotFound);
        }

        WebhookSigningKeyRotatedResult result = await keyWriter
            .RotateSigningKeyAsync(id, retiredKeyGracePeriod: null, cancellationToken)
            .ConfigureAwait(false);

        WebhookSigningKey? newKey = await keyReader
            .FindByIdAsync(result.KeyId, cancellationToken).ConfigureAwait(false);

        DateTimeOffset createdAt = newKey?.CreatedAt ?? clock.Now;

        var response = new WebhookSigningKeyCreatedResponse(
            result.KeyId,
            id,
            createdAt,
            result.PlainSecret);

        return TypedResults.Created($"/subscriptions/{id}/keys/{result.KeyId}", response);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RevokeKey(
        Guid id,
        Guid keyId,
        [FromServices] IWebhookSigningKeyWriter keyWriter,
        CancellationToken cancellationToken)
    {
        try
        {
            await keyWriter.RevokeSigningKeyAsync(id, keyId, cancellationToken).ConfigureAwait(false);
            return TypedResults.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (EntityNotFoundException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }
}
