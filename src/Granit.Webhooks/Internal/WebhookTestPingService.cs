using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Granit.Guids;
using Granit.Timing;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;

namespace Granit.Webhooks.Internal;

/// <summary>
/// Default implementation of <see cref="IWebhookTestPingService"/>.
/// Sends a <c>webhook.test</c> event envelope to the subscription's target URL
/// and measures round-trip latency.
/// </summary>
internal sealed class WebhookTestPingService(
    IWebhookSubscriptionReader subscriptionReader,
    IWebhookSecretProtector secretProtector,
    IHttpClientFactory httpClientFactory,
    IGuidGenerator guidGenerator,
    IClock clock) : IWebhookTestPingService
{
    private readonly IWebhookSubscriptionReader _subscriptionReader = subscriptionReader;
    private readonly IWebhookSecretProtector _secretProtector = secretProtector;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IGuidGenerator _guidGenerator = guidGenerator;
    private readonly IClock _clock = clock;

    public async Task<WebhookTestPingResult> SendTestPingAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        WebhookSubscription? subscription = await _subscriptionReader
            .FindByIdAsync(subscriptionId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            throw new Granit.Exceptions.EntityNotFoundException(typeof(WebhookSubscription), subscriptionId);
        }

        string plainSecret = await _secretProtector
            .UnprotectAsync(subscription.SigningSecret, cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = _clock.Now;
        var testEnvelope = new
        {
            eventId = _guidGenerator.Create(),
            eventType = "webhook.test",
            tenantId = (Guid?)null,
            timestamp = now,
            apiVersion = WebhooksConstants.ApiVersion,
            data = new { message = "Test ping from Granit webhook administration." },
        };

        string bodyJson = JsonSerializer.Serialize(testEnvelope);
        string signature = WebhookSignatureService.Compute(plainSecret, now, bodyJson);

        using var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, subscription.TargetUrl)
        {
            Content = content,
        };
        request.Headers.TryAddWithoutValidation("x-granit-signature", signature);
        request.Headers.TryAddWithoutValidation("x-granit-event-id", testEnvelope.eventId.ToString());
        request.Headers.TryAddWithoutValidation("x-granit-event-type", testEnvelope.eventType);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using HttpClient client = _httpClientFactory.CreateClient(WebhooksConstants.HttpClientName);
            using HttpResponseMessage response = await client
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            stopwatch.Stop();
            return new WebhookTestPingResult(response.IsSuccessStatusCode, (int)response.StatusCode, stopwatch.ElapsedMilliseconds);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new WebhookTestPingResult(false, 0, stopwatch.ElapsedMilliseconds);
        }
    }
}
