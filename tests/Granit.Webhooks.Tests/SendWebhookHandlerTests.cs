// =============================================================================
// Tests - SendWebhookHandler
// =============================================================================
// Verifies HTTP delivery: success path, non-retriable errors (suspension),
// retriable errors (exception thrown), network timeout handling, StorePayload.
// =============================================================================

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json;
using Granit.Timing;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Diagnostics;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Exceptions;
using Granit.Webhooks.Handlers;
using Granit.Webhooks.Internal;
using Granit.Webhooks.Messages;
using Granit.Webhooks.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Tests;

public sealed class SendWebhookHandlerTests : IDisposable
{
    private readonly IWebhookDeliveryWriter _deliveryWriter = Substitute.For<IWebhookDeliveryWriter>();
    private readonly IWebhookSubscriptionReader _subscriptionReader = Substitute.For<IWebhookSubscriptionReader>();

    // Use the real no-op protector to avoid CA2012 when mocking ValueTask-returning methods.
    private readonly IWebhookSecretProtector _secretProtector = new NoOpWebhookSecretProtector();

    private readonly IClock _clock;
    private readonly ActivityListener _activityListener;
    private readonly ServiceProvider _sp;
    private readonly WebhooksMetrics _metrics;

    public SendWebhookHandlerTests()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Granit.Webhooks",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_activityListener);

        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _metrics = new WebhooksMetrics(_sp.GetRequiredService<IMeterFactory>());

        _clock = Substitute.For<IClock>();
        _clock.Now.Returns(_ => DateTimeOffset.UtcNow);

        _subscriptionReader.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => WebhookSubscription.Create(
                ci.ArgAt<Guid>(0), "https://example.com/webhook", "test.event",
                signingKeyId: Guid.NewGuid(), protectedSecret: "test-secret", createdAt: DateTimeOffset.UtcNow));
    }

    public void Dispose()
    {
        _activityListener.Dispose();
        _sp.Dispose();
    }

    // -------------------------------------------------------------------------
    // Success
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_Http2xx_CallsRecordSuccess()
    {
        SendWebhookHandler handler = BuildHandler(HttpStatusCode.OK);
        SendWebhookCommand command = BuildCommand();

        await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        await _deliveryWriter.Received(1).RecordSuccessAsync(
            command, 200, Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Http201_DoesNotThrow()
    {
        SendWebhookHandler handler = BuildHandler(HttpStatusCode.Created);

        Func<Task> act = () => handler.HandleAsync(BuildCommand(), TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    // -------------------------------------------------------------------------
    // Non-retriable: return without throw, record failure
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.MethodNotAllowed)]
    public async Task HandleAsync_NonRetriableNonSuspending_RecordsFailureAndReturns(HttpStatusCode statusCode)
    {
        SendWebhookHandler handler = BuildHandler(statusCode);
        SendWebhookCommand command = BuildCommand();

        Func<Task> act = () => handler.HandleAsync(command, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
        await _deliveryWriter.Received(1).RecordFailureAsync(
            command, (int)statusCode, Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _deliveryWriter.DidNotReceive().SuspendSubscriptionAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task HandleAsync_NonRetriableSuspending_SuspendsSubscription(HttpStatusCode statusCode)
    {
        SendWebhookHandler handler = BuildHandler(statusCode);
        SendWebhookCommand command = BuildCommand();

        await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        await _deliveryWriter.Received(1).SuspendSubscriptionAsync(
            command.SubscriptionId, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Retriable: throw WebhookDeliveryException
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task HandleAsync_RetriableHttpError_ThrowsWebhookDeliveryException(HttpStatusCode statusCode)
    {
        SendWebhookHandler handler = BuildHandler(statusCode);

        Func<Task> act = () => handler.HandleAsync(BuildCommand(), TestContext.Current.CancellationToken);

        await Should.ThrowAsync<WebhookDeliveryException>(act);
    }

    [Fact]
    public async Task HandleAsync_RetriableError_RecordsFailureBeforeThrow()
    {
        SendWebhookHandler handler = BuildHandler(HttpStatusCode.ServiceUnavailable);
        SendWebhookCommand command = BuildCommand();

        await Assert.ThrowsAsync<WebhookDeliveryException>(
            () => handler.HandleAsync(command, TestContext.Current.CancellationToken));

        await _deliveryWriter.Received(1).RecordFailureAsync(
            command, 503, Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Timeout
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_NetworkTimeout_ThrowsWebhookDeliveryException()
    {
        SendWebhookHandler handler = BuildHandlerWithTimeout();
        SendWebhookCommand command = BuildCommand();

        Func<Task> act = () => handler.HandleAsync(command, TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<WebhookDeliveryException>(act)).Message.ShouldContain("Timeout");
    }

    [Fact]
    public async Task HandleAsync_NetworkTimeout_RecordsFailureWithNullStatusCode()
    {
        SendWebhookHandler handler = BuildHandlerWithTimeout();
        SendWebhookCommand command = BuildCommand();

        await Assert.ThrowsAsync<WebhookDeliveryException>(
            () => handler.HandleAsync(command, TestContext.Current.CancellationToken));

        await _deliveryWriter.Received(1).RecordFailureAsync(
            command, null, Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // StorePayload
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_StorePayloadEnabled_PassesBodyJsonToRecordSuccess()
    {
        SendWebhookHandler handler = BuildHandler(HttpStatusCode.OK, storePayload: true);
        SendWebhookCommand command = BuildCommand();

        await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        await _deliveryWriter.Received(1).RecordSuccessAsync(
            command, 200, Arg.Any<long>(), Arg.Any<string>(),
            Arg.Is<string?>(p => p != null && p.Contains(command.Envelope.EventType)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_StorePayloadDisabled_PassesNullPayload()
    {
        SendWebhookHandler handler = BuildHandler(HttpStatusCode.OK, storePayload: false);
        SendWebhookCommand command = BuildCommand();

        await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        await _deliveryWriter.Received(1).RecordSuccessAsync(
            command, 200, Arg.Any<long>(), Arg.Any<string>(),
            Arg.Is<string?>(p => p == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_StorePayloadEnabled_FailureAlsoStoresPayload()
    {
        SendWebhookHandler handler = BuildHandler(HttpStatusCode.BadRequest, storePayload: true);
        SendWebhookCommand command = BuildCommand();

        await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        await _deliveryWriter.Received(1).RecordFailureAsync(
            command, 400, Arg.Any<long>(), Arg.Any<string>(),
            Arg.Is<string?>(p => p != null),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private SendWebhookHandler BuildHandler(HttpStatusCode statusCode, bool storePayload = false)
    {
        HttpClient httpClient = new(new StaticResponseHandler(statusCode));
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(httpClient);
        IOptions<WebhooksOptions> opts = Microsoft.Extensions.Options.Options.Create(new WebhooksOptions { StorePayload = storePayload });
        return new SendWebhookHandler(factory, _deliveryWriter, _subscriptionReader, _secretProtector, opts, NullLogger<SendWebhookHandler>.Instance, _clock, _metrics);
    }

    private SendWebhookHandler BuildHandlerWithTimeout()
    {
        HttpClient httpClient = new(new TimeoutHandler());
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(httpClient);
        IOptions<WebhooksOptions> opts = Microsoft.Extensions.Options.Options.Create(new WebhooksOptions());
        return new SendWebhookHandler(factory, _deliveryWriter, _subscriptionReader, _secretProtector, opts, NullLogger<SendWebhookHandler>.Instance, _clock, _metrics);
    }

    private static SendWebhookCommand BuildCommand() => new()
    {
        DeliveryId = Guid.NewGuid(),
        SubscriptionId = Guid.NewGuid(),
        TargetUrl = "https://example.com/webhook",
        Envelope = new WebhookEnvelope
        {
            EventId = Guid.NewGuid(),
            EventType = "test.event",
            TenantId = null,
            Timestamp = DateTimeOffset.UtcNow,
            ApiVersion = "2025-01-01",
            Data = JsonSerializer.SerializeToElement(new { key = "value" }),
        },
    };

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed class StaticResponseHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode));
    }

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new TaskCanceledException("Simulated network timeout");
    }
}
