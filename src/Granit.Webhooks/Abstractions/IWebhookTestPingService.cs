namespace Granit.Webhooks.Abstractions;

/// <summary>
/// Sends a test webhook to a subscription's target URL and reports the result.
/// </summary>
public interface IWebhookTestPingService
{
    /// <summary>
    /// Sends a test ping to the subscription's target URL using a <c>webhook.test</c> event envelope.
    /// </summary>
    /// <param name="subscriptionId">Target subscription identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The test result including HTTP status code and latency.</returns>
    Task<WebhookTestPingResult> SendTestPingAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
}
