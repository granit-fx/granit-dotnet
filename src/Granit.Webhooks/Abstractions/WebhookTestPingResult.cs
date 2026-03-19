namespace Granit.Webhooks.Abstractions;

/// <summary>
/// Result of a test webhook ping sent to a subscription's target URL.
/// </summary>
/// <param name="Success">Whether the target returned a 2xx response.</param>
/// <param name="HttpStatusCode">HTTP status code received, or <c>0</c> on timeout.</param>
/// <param name="DurationMs">Round-trip latency in milliseconds.</param>
public sealed record WebhookTestPingResult(bool Success, int HttpStatusCode, long DurationMs);
