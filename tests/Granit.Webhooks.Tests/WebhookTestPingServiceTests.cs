using System.Net;
using Granit.Guids;
using Granit.Timing;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Tests;

public sealed class WebhookTestPingServiceTests : IDisposable
{
    private readonly IWebhookSubscriptionReader _subscriptionReader = Substitute.For<IWebhookSubscriptionReader>();
    private readonly IWebhookSecretProtector _secretProtector = new NoOpWebhookSecretProtector();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly DateTimeOffset _fixedTime = new(2026, 3, 15, 10, 0, 0, TimeSpan.Zero);
    private readonly Guid _subscriptionId = Guid.NewGuid();
    private readonly Guid _eventId = Guid.NewGuid();
    private HttpClient? _httpClient;

    public WebhookTestPingServiceTests()
    {
        _clock.Now.Returns(_fixedTime);
        _guidGenerator.Create().Returns(_eventId);
    }

    public void Dispose() => _httpClient?.Dispose();

    private WebhookTestPingService CreateService(HttpStatusCode statusCode)
    {
        _httpClient = new HttpClient(new StaticResponseHandler(statusCode));
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_httpClient);

        return new WebhookTestPingService(
            _subscriptionReader,
            _secretProtector,
            factory,
            _guidGenerator,
            _clock);
    }

    private WebhookTestPingService CreateServiceWithTimeout()
    {
        _httpClient = new HttpClient(new TimeoutHandler());
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_httpClient);

        return new WebhookTestPingService(
            _subscriptionReader,
            _secretProtector,
            factory,
            _guidGenerator,
            _clock);
    }

    private void SetupSubscription(string targetUrl = "https://example.com/webhook", string secret = "test-secret")
    {
        var subscription = WebhookSubscription.Create(
            _subscriptionId, targetUrl, "webhook.test", secret);

        _subscriptionReader.FindByIdAsync(_subscriptionId, Arg.Any<CancellationToken>())
            .Returns(subscription);
    }

    // =========================================================================
    // Happy path — HTTP 200
    // =========================================================================

    [Fact]
    public async Task SendTestPingAsync_SuccessResponse_ReturnsSuccessResult()
    {
        SetupSubscription();
        WebhookTestPingService service = CreateService(HttpStatusCode.OK);

        WebhookTestPingResult result = await service.SendTestPingAsync(
            _subscriptionId, TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.HttpStatusCode.ShouldBe(200);
        result.DurationMs.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task SendTestPingAsync_Http201_ReturnsSuccessResult()
    {
        SetupSubscription();
        WebhookTestPingService service = CreateService(HttpStatusCode.Created);

        WebhookTestPingResult result = await service.SendTestPingAsync(
            _subscriptionId, TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.HttpStatusCode.ShouldBe(201);
    }

    // =========================================================================
    // Failure responses
    // =========================================================================

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400)]
    [InlineData(HttpStatusCode.Unauthorized, 401)]
    [InlineData(HttpStatusCode.Forbidden, 403)]
    [InlineData(HttpStatusCode.NotFound, 404)]
    [InlineData(HttpStatusCode.InternalServerError, 500)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 503)]
    public async Task SendTestPingAsync_ErrorResponse_ReturnsFailureResult(HttpStatusCode statusCode, int expectedCode)
    {
        SetupSubscription();
        WebhookTestPingService service = CreateService(statusCode);

        WebhookTestPingResult result = await service.SendTestPingAsync(
            _subscriptionId, TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.HttpStatusCode.ShouldBe(expectedCode);
        result.DurationMs.ShouldBeGreaterThanOrEqualTo(0);
    }

    // =========================================================================
    // Subscription not found
    // =========================================================================

    [Fact]
    public async Task SendTestPingAsync_SubscriptionNotFound_ThrowsEntityNotFoundException()
    {
        _subscriptionReader.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((WebhookSubscription?)null);

        WebhookTestPingService service = CreateService(HttpStatusCode.OK);

        Func<Task> act = () => service.SendTestPingAsync(
            _subscriptionId, TestContext.Current.CancellationToken);

        Granit.Exceptions.EntityNotFoundException ex =
            await Should.ThrowAsync<Granit.Exceptions.EntityNotFoundException>(act);
        ex.EntityType.ShouldBe(typeof(WebhookSubscription));
    }

    // =========================================================================
    // Timeout
    // =========================================================================

    [Fact]
    public async Task SendTestPingAsync_Timeout_ReturnsFailureWithZeroStatusCode()
    {
        SetupSubscription();
        WebhookTestPingService service = CreateServiceWithTimeout();

        WebhookTestPingResult result = await service.SendTestPingAsync(
            _subscriptionId, TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.HttpStatusCode.ShouldBe(0);
        result.DurationMs.ShouldBeGreaterThanOrEqualTo(0);
    }

    // =========================================================================
    // Request structure verification
    // =========================================================================

    [Fact]
    public async Task SendTestPingAsync_SendsPostRequest()
    {
        SetupSubscription();
        var capturingHandler = new RequestCapturingHandler(HttpStatusCode.OK);
        _httpClient = new HttpClient(capturingHandler);
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_httpClient);

        var service = new WebhookTestPingService(
            _subscriptionReader, _secretProtector, factory, _guidGenerator, _clock);

        await service.SendTestPingAsync(_subscriptionId, TestContext.Current.CancellationToken);

        capturingHandler.CapturedMethod.ShouldBe(HttpMethod.Post);
    }

    [Fact]
    public async Task SendTestPingAsync_SetsSignatureHeader()
    {
        SetupSubscription();
        var capturingHandler = new RequestCapturingHandler(HttpStatusCode.OK);
        _httpClient = new HttpClient(capturingHandler);
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_httpClient);

        var service = new WebhookTestPingService(
            _subscriptionReader, _secretProtector, factory, _guidGenerator, _clock);

        await service.SendTestPingAsync(_subscriptionId, TestContext.Current.CancellationToken);

        capturingHandler.CapturedHeaders.ShouldContainKey("x-granit-signature");
    }

    [Fact]
    public async Task SendTestPingAsync_SetsEventIdHeader()
    {
        SetupSubscription();
        var capturingHandler = new RequestCapturingHandler(HttpStatusCode.OK);
        _httpClient = new HttpClient(capturingHandler);
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_httpClient);

        var service = new WebhookTestPingService(
            _subscriptionReader, _secretProtector, factory, _guidGenerator, _clock);

        await service.SendTestPingAsync(_subscriptionId, TestContext.Current.CancellationToken);

        capturingHandler.CapturedHeaders.ShouldContainKey("x-granit-event-id");
        string eventIdHeader = capturingHandler.CapturedHeaders["x-granit-event-id"];
        eventIdHeader.ShouldBe(_eventId.ToString());
    }

    [Fact]
    public async Task SendTestPingAsync_SetsEventTypeHeader()
    {
        SetupSubscription();
        var capturingHandler = new RequestCapturingHandler(HttpStatusCode.OK);
        _httpClient = new HttpClient(capturingHandler);
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_httpClient);

        var service = new WebhookTestPingService(
            _subscriptionReader, _secretProtector, factory, _guidGenerator, _clock);

        await service.SendTestPingAsync(_subscriptionId, TestContext.Current.CancellationToken);

        capturingHandler.CapturedHeaders.ShouldContainKey("x-granit-event-type");
        string eventTypeHeader = capturingHandler.CapturedHeaders["x-granit-event-type"];
        eventTypeHeader.ShouldBe("webhook.test");
    }

    [Fact]
    public async Task SendTestPingAsync_BodyContainsWebhookTestEventType()
    {
        SetupSubscription();
        var capturingHandler = new RequestCapturingHandler(HttpStatusCode.OK);
        _httpClient = new HttpClient(capturingHandler);
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_httpClient);

        var service = new WebhookTestPingService(
            _subscriptionReader, _secretProtector, factory, _guidGenerator, _clock);

        await service.SendTestPingAsync(_subscriptionId, TestContext.Current.CancellationToken);

        capturingHandler.CapturedBody.ShouldNotBeNullOrWhiteSpace();
        capturingHandler.CapturedBody!.ShouldContain("\"eventType\":\"webhook.test\"");
    }

    [Fact]
    public async Task SendTestPingAsync_BodyContainsApiVersion()
    {
        SetupSubscription();
        var capturingHandler = new RequestCapturingHandler(HttpStatusCode.OK);
        _httpClient = new HttpClient(capturingHandler);
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_httpClient);

        var service = new WebhookTestPingService(
            _subscriptionReader, _secretProtector, factory, _guidGenerator, _clock);

        await service.SendTestPingAsync(_subscriptionId, TestContext.Current.CancellationToken);

        capturingHandler.CapturedBody!.ShouldContain("\"apiVersion\":\"2025-01-01\"");
    }

    [Fact]
    public async Task SendTestPingAsync_BodyContainsTestPingMessage()
    {
        SetupSubscription();
        var capturingHandler = new RequestCapturingHandler(HttpStatusCode.OK);
        _httpClient = new HttpClient(capturingHandler);
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_httpClient);

        var service = new WebhookTestPingService(
            _subscriptionReader, _secretProtector, factory, _guidGenerator, _clock);

        await service.SendTestPingAsync(_subscriptionId, TestContext.Current.CancellationToken);

        capturingHandler.CapturedBody!.ShouldContain("Test ping from Granit webhook administration.");
    }

    [Fact]
    public async Task SendTestPingAsync_SignatureMatchesComputedValue()
    {
        const string secret = "test-signing-secret";
        SetupSubscription(secret: secret);
        var capturingHandler = new RequestCapturingHandler(HttpStatusCode.OK);
        _httpClient = new HttpClient(capturingHandler);
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_httpClient);

        var service = new WebhookTestPingService(
            _subscriptionReader, _secretProtector, factory, _guidGenerator, _clock);

        await service.SendTestPingAsync(_subscriptionId, TestContext.Current.CancellationToken);

        string expectedSignature = WebhookSignatureService.Compute(secret, _fixedTime, capturingHandler.CapturedBody!);
        string actualSignature = capturingHandler.CapturedHeaders["x-granit-signature"];
        actualSignature.ShouldBe(expectedSignature);
    }

    [Fact]
    public async Task SendTestPingAsync_UsesCorrectTargetUrl()
    {
        const string targetUrl = "https://hooks.example.org/incoming";
        SetupSubscription(targetUrl: targetUrl);
        var capturingHandler = new RequestCapturingHandler(HttpStatusCode.OK);
        _httpClient = new HttpClient(capturingHandler);
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_httpClient);

        var service = new WebhookTestPingService(
            _subscriptionReader, _secretProtector, factory, _guidGenerator, _clock);

        await service.SendTestPingAsync(_subscriptionId, TestContext.Current.CancellationToken);

        capturingHandler.CapturedUri.ShouldNotBeNull();
        capturingHandler.CapturedUri!.ToString().ShouldBe(targetUrl);
    }

    [Fact]
    public async Task SendTestPingAsync_UsesNamedHttpClient()
    {
        SetupSubscription();
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        _httpClient = new HttpClient(new StaticResponseHandler(HttpStatusCode.OK));
        factory.CreateClient(Arg.Any<string>()).Returns(_httpClient);

        var service = new WebhookTestPingService(
            _subscriptionReader, _secretProtector, factory, _guidGenerator, _clock);

        await service.SendTestPingAsync(_subscriptionId, TestContext.Current.CancellationToken);

        factory.Received(1).CreateClient(WebhooksConstants.HttpClientName);
    }

    // =========================================================================
    // Test doubles
    // =========================================================================

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

    private sealed class RequestCapturingHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public HttpMethod? CapturedMethod { get; private set; }
        public Uri? CapturedUri { get; private set; }
        public Dictionary<string, string> CapturedHeaders { get; } = [];
        public string? CapturedBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CapturedMethod = request.Method;
            CapturedUri = request.RequestUri;

            foreach (System.Collections.Generic.KeyValuePair<string, IEnumerable<string>> header in request.Headers)
            {
                CapturedHeaders[header.Key] = string.Join(",", header.Value);
            }

            if (request.Content is not null)
            {
                CapturedBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(statusCode);
        }
    }
}
