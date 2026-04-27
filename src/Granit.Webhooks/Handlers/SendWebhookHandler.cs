using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Granit.Timing;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Diagnostics;
using Granit.Webhooks.Exceptions;
using Granit.Webhooks.Internal;
using Granit.Webhooks.Messages;
using Granit.Webhooks.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Webhooks.Handlers;

/// <summary>
/// Wolverine handler that delivers a <see cref="SendWebhookCommand"/> via HTTP POST.
/// </summary>
/// <remarks>
/// <para>
/// Two error paths are distinguished to avoid unnecessary retries:
/// <list type="bullet">
///   <item>
///     <b>Non-retriable HTTP errors</b> (400, 401, 403, 404, 405, 410, 422): the handler
///     records the failure and returns without throwing — Wolverine considers the message
///     successfully processed. Subscriptions that received 401/403/404/410 are automatically
///     suspended to prevent further wasted attempts.
///   </item>
///   <item>
///     <b>Retriable errors</b> (429, 5xx, network timeout): the handler throws
///     <see cref="WebhookDeliveryException"/>, which triggers Wolverine's durable exponential
///     backoff (30 s → 2 min → 10 min → 30 min → 2 h → 12 h → Dead-Letter Queue).
///   </item>
/// </list>
/// </para>
/// <para>
/// The signing secret is unprotected at delivery time via <see cref="IWebhookSecretProtector"/>.
/// The secret value is never logged — only <see cref="SendWebhookCommand.SubscriptionId"/>
/// and <see cref="SendWebhookCommand.DeliveryId"/> are used for correlation.
/// </para>
/// </remarks>
public sealed partial class SendWebhookHandler(
    IHttpClientFactory httpClientFactory,
    IWebhookDeliveryWriter deliveryWriter,
    IWebhookSubscriptionReader subscriptionReader,
    IWebhookSecretProtector secretProtector,
    IOptions<WebhooksOptions> options,
    ILogger<SendWebhookHandler> logger,
    IClock clock,
    WebhooksMetrics metrics)
{
    /// <summary>
    /// Executes the HTTP POST delivery. Throws <see cref="WebhookDeliveryException"/> on
    /// retriable errors; returns normally on non-retriable errors (idempotent failure path).
    /// </summary>
    public async Task HandleAsync(SendWebhookCommand command, CancellationToken cancellationToken)
    {
        using Activity? activity = WebhooksActivitySource.Source.StartActivity(WebhooksActivitySource.Deliver);
        activity?.SetTag("webhooks.subscription_id", command.SubscriptionId.ToString());
        activity?.SetTag("webhooks.delivery_id", command.DeliveryId.ToString());
        activity?.SetTag("webhooks.event_type", command.Envelope.EventType);

        string bodyJson = JsonSerializer.Serialize(command.Envelope);
        string payloadHash = WebhookSignatureService.ComputePayloadHash(bodyJson);
        string? storedPayload = options.Value.StorePayload ? bodyJson : null;
        DateTimeOffset sentAt = clock.Now;

        // Resolve the signing secret at delivery time — never carried in outbox messages.
        Domain.WebhookSubscription? subscription = await subscriptionReader
            .FindByIdAsync(command.SubscriptionId, cancellationToken).ConfigureAwait(false);

        if (subscription is null || subscription.Status == Domain.WebhookSubscriptionStatus.Deactivated)
        {
            LogSubscriptionGone(command.SubscriptionId, command.DeliveryId);
            await deliveryWriter.RecordFailureAsync(
                command, httpStatusCode: null, durationMs: 0,
                "Subscription not found or deactivated at delivery time", storedPayload, cancellationToken).ConfigureAwait(false);
            return; // Treat as non-retriable — subscription is gone.
        }

        string plainSecret = await WebhookSecretResolver
            .ResolvePlainSecretAsync(subscription, secretProtector, cancellationToken)
            .ConfigureAwait(false);
        string signature = WebhookSignatureService.Compute(plainSecret, sentAt, bodyJson);

        using StringContent content = new(bodyJson, Encoding.UTF8, "application/json");
        using HttpRequestMessage request = new(HttpMethod.Post, command.TargetUrl)
        {
            Content = content,
        };
        request.Headers.TryAddWithoutValidation("x-granit-signature", signature);
        request.Headers.TryAddWithoutValidation("x-granit-event-id", command.Envelope.EventId.ToString());
        request.Headers.TryAddWithoutValidation("x-granit-event-type", command.Envelope.EventType);

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response;

        try
        {
            using HttpClient client = httpClientFactory.CreateClient(WebhooksConstants.HttpClientName);
            response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Network timeout — not a cancellation from the application.
            stopwatch.Stop();
            string timeoutMessage = $"Timeout delivering to {command.TargetUrl}";
            LogWebhookTimeout(ex, command.SubscriptionId, command.DeliveryId);

            await deliveryWriter.RecordFailureAsync(
                command, httpStatusCode: null, stopwatch.ElapsedMilliseconds, timeoutMessage, storedPayload, cancellationToken).ConfigureAwait(false);

            string? tenant = command.Envelope.TenantId?.ToString();
            metrics.RecordDeliveryFailed(tenant, command.Envelope.EventType, httpStatus: null);
            metrics.RecordDeliveryDuration(tenant, command.Envelope.EventType, "timeout", stopwatch.Elapsed);

            activity?.SetStatus(ActivityStatusCode.Error, timeoutMessage);
            throw new WebhookDeliveryException(timeoutMessage, ex);
        }

        stopwatch.Stop();
        int statusCode = (int)response.StatusCode;
        activity?.SetTag("http.response.status_code", statusCode);

        if (IsNonRetriable(response.StatusCode))
        {
            LogNonRetriableHttpError(statusCode, command.SubscriptionId, command.DeliveryId);
            activity?.SetStatus(ActivityStatusCode.Error, $"Non-retriable HTTP {statusCode}");

            await deliveryWriter.RecordFailureAsync(
                command, statusCode, stopwatch.ElapsedMilliseconds,
                $"Non-retriable HTTP {statusCode}", storedPayload, cancellationToken).ConfigureAwait(false);

            string? nonRetriableTenant = command.Envelope.TenantId?.ToString();
            metrics.RecordDeliveryFailed(nonRetriableTenant, command.Envelope.EventType, statusCode);
            metrics.RecordDeliveryDuration(nonRetriableTenant, command.Envelope.EventType, "failed", stopwatch.Elapsed);

            if (ShouldSuspend(response.StatusCode))
            {
                await deliveryWriter.SuspendSubscriptionAsync(
                    command.SubscriptionId,
                    $"Auto-suspended: HTTP {statusCode}",
                    cancellationToken).ConfigureAwait(false);

                metrics.RecordSubscriptionSuspended(nonRetriableTenant, statusCode);
            }

            return; // Message considered processed — no retry.
        }

        if (!response.IsSuccessStatusCode)
        {
            // Retriable: 429, 5xx.
            string retriableMessage = $"HTTP {statusCode} from {command.TargetUrl}";
            LogRetriableHttpError(statusCode, command.SubscriptionId, command.DeliveryId);

            await deliveryWriter.RecordFailureAsync(
                command, statusCode, stopwatch.ElapsedMilliseconds, retriableMessage, storedPayload, cancellationToken).ConfigureAwait(false);

            string? retriableTenant = command.Envelope.TenantId?.ToString();
            metrics.RecordDeliveryFailed(retriableTenant, command.Envelope.EventType, statusCode);
            metrics.RecordDeliveryDuration(retriableTenant, command.Envelope.EventType, "failed", stopwatch.Elapsed);

            activity?.SetStatus(ActivityStatusCode.Error, retriableMessage);
            throw new WebhookDeliveryException(retriableMessage);
        }

        // Success 2xx.
        LogWebhookDelivered(statusCode, command.SubscriptionId, command.DeliveryId);

        await deliveryWriter.RecordSuccessAsync(
            command, statusCode, stopwatch.ElapsedMilliseconds, payloadHash, storedPayload, cancellationToken).ConfigureAwait(false);

        string? successTenant = command.Envelope.TenantId?.ToString();
        metrics.RecordDeliverySucceeded(successTenant, command.Envelope.EventType);
        metrics.RecordDeliveryDuration(successTenant, command.Envelope.EventType, "succeeded", stopwatch.Elapsed);
    }

    /// <summary>
    /// Returns <c>true</c> for HTTP status codes that indicate a permanent, deterministic failure
    /// that will not be resolved by retrying.
    /// </summary>
    private static bool IsNonRetriable(HttpStatusCode code) => code is
        HttpStatusCode.BadRequest or  // 400 — malformed payload on the client side
        HttpStatusCode.Unauthorized or  // 401 — signing secret revoked
        HttpStatusCode.Forbidden or  // 403 — endpoint no longer authorized
        HttpStatusCode.NotFound or  // 404 — endpoint has disappeared
        HttpStatusCode.MethodNotAllowed or  // 405 — POST no longer accepted
        HttpStatusCode.Gone or  // 410 — endpoint explicitly retired
        HttpStatusCode.UnprocessableEntity;      // 422 — client rejects the payload format permanently

    /// <summary>
    /// Returns <c>true</c> for codes that indicate the subscription endpoint is permanently
    /// unreachable or unauthorized — the subscription should be suspended to avoid
    /// polluting the cluster with futile attempts.
    /// </summary>
    private static bool ShouldSuspend(HttpStatusCode code) => code is
        HttpStatusCode.Unauthorized or
        HttpStatusCode.Forbidden or
        HttpStatusCode.NotFound or
        HttpStatusCode.Gone;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook delivery timeout for subscription {SubscriptionId} delivery {DeliveryId}")]
    private partial void LogWebhookTimeout(Exception exception, Guid subscriptionId, Guid deliveryId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Non-retriable HTTP {StatusCode} for subscription {SubscriptionId} delivery {DeliveryId}")]
    private partial void LogNonRetriableHttpError(int statusCode, Guid subscriptionId, Guid deliveryId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Retriable HTTP {StatusCode} for subscription {SubscriptionId} delivery {DeliveryId}")]
    private partial void LogRetriableHttpError(int statusCode, Guid subscriptionId, Guid deliveryId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Webhook delivered successfully HTTP {StatusCode} for subscription {SubscriptionId} delivery {DeliveryId}")]
    private partial void LogWebhookDelivered(int statusCode, Guid subscriptionId, Guid deliveryId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Subscription {SubscriptionId} not found or deactivated at delivery time for delivery {DeliveryId}")]
    private partial void LogSubscriptionGone(Guid subscriptionId, Guid deliveryId);
}
