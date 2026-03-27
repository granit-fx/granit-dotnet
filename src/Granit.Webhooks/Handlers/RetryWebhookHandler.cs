using System.Text.Json;
using Granit.Guids;
using Granit.Timing;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Internal;
using Granit.Webhooks.Messages;

namespace Granit.Webhooks.Handlers;

/// <summary>
/// Validates and prepares a redelivery of a previously failed webhook delivery attempt.
/// </summary>
/// <remarks>
/// <para>
/// This is a service called by admin endpoints — not a Wolverine message handler.
/// It validates that the original attempt exists, was not successful, and that the subscription
/// is still eligible for delivery before building a new <see cref="SendWebhookCommand"/>.
/// </para>
/// <para>
/// The caller is responsible for publishing the returned <see cref="SendWebhookCommand"/>
/// into the Wolverine Outbox.
/// </para>
/// </remarks>
public sealed class RetryWebhookHandler(
    IWebhookDeliveryReader deliveryReader,
    IWebhookSubscriptionReader subscriptionReader,
    IGuidGenerator guidGenerator,
    IClock clock)
{
    /// <summary>
    /// Validates the retry request and builds a new <see cref="SendWebhookCommand"/>.
    /// </summary>
    /// <returns>A successful result with the command, or a failure result with error details.</returns>
    public async Task<RetryWebhookResult> HandleAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        WebhookDeliveryAttempt? attempt = await deliveryReader
            .FindByDeliveryIdAsync(deliveryId, cancellationToken).ConfigureAwait(false);

        if (attempt is null)
        {
            return RetryWebhookResult.NotFound($"Delivery attempt '{deliveryId}' not found.");
        }

        if (attempt.IsSuccess)
        {
            return RetryWebhookResult.InvalidRequest("Cannot retry a successful delivery attempt.");
        }

        WebhookSubscription? subscription = await subscriptionReader
            .FindByIdAsync(attempt.SubscriptionId, cancellationToken).ConfigureAwait(false);

        if (subscription is null)
        {
            return RetryWebhookResult.NotFound($"Subscription '{attempt.SubscriptionId}' not found.");
        }

        if (subscription.Status == WebhookSubscriptionStatus.Deactivated)
        {
            return RetryWebhookResult.Conflict("Cannot retry delivery for a deactivated subscription.");
        }

        var command = new SendWebhookCommand
        {
            DeliveryId = guidGenerator.Create(),
            SubscriptionId = subscription.Id,
            TargetUrl = subscription.TargetUrl,
            Envelope = new WebhookEnvelope
            {
                EventId = attempt.DeliveryId,
                EventType = attempt.EventType,
                TenantId = attempt.TenantId,
                Timestamp = clock.Now,
                ApiVersion = WebhooksConstants.ApiVersion,
                Data = DeserializePayloadData(attempt.Payload),
            },
        };

        return RetryWebhookResult.Success(command);
    }

    private static JsonElement DeserializePayloadData(string? payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return JsonSerializer.SerializeToElement(new { });
        }

        using var doc = JsonDocument.Parse(payload);
        if (doc.RootElement.TryGetProperty("Data", out JsonElement data)
            || doc.RootElement.TryGetProperty("data", out data))
        {
            return data.Clone();
        }

        return JsonSerializer.SerializeToElement(new { });
    }
}

/// <summary>
/// Result of a <see cref="RetryWebhookHandler.HandleAsync"/> validation.
/// </summary>
public sealed class RetryWebhookResult
{
    private RetryWebhookResult(bool isSuccess, SendWebhookCommand? command, string? error, RetryWebhookErrorKind errorKind)
    {
        IsSuccess = isSuccess;
        Command = command;
        Error = error;
        ErrorKind = errorKind;
    }

    /// <summary>Whether the retry request was validated successfully.</summary>
    public bool IsSuccess { get; }

    /// <summary>The <see cref="SendWebhookCommand"/> to publish. Non-null when <see cref="IsSuccess"/> is <c>true</c>.</summary>
    public SendWebhookCommand? Command { get; }

    /// <summary>Human-readable error message. Non-null when <see cref="IsSuccess"/> is <c>false</c>.</summary>
    public string? Error { get; }

    /// <summary>Error category for HTTP status code mapping.</summary>
    public RetryWebhookErrorKind ErrorKind { get; }

    internal static RetryWebhookResult Success(SendWebhookCommand command) => new(true, command, null, RetryWebhookErrorKind.None);
    internal static RetryWebhookResult NotFound(string error) => new(false, null, error, RetryWebhookErrorKind.NotFound);
    internal static RetryWebhookResult InvalidRequest(string error) => new(false, null, error, RetryWebhookErrorKind.InvalidRequest);
    internal static RetryWebhookResult Conflict(string error) => new(false, null, error, RetryWebhookErrorKind.Conflict);
}

/// <summary>
/// Error category for <see cref="RetryWebhookResult"/>.
/// </summary>
public enum RetryWebhookErrorKind
{
    /// <summary>No error.</summary>
    None = 0,

    /// <summary>The delivery attempt or subscription was not found (404).</summary>
    NotFound = 1,

    /// <summary>The delivery attempt is not eligible for retry (400).</summary>
    InvalidRequest = 2,

    /// <summary>The subscription is deactivated (409).</summary>
    Conflict = 3,
}
