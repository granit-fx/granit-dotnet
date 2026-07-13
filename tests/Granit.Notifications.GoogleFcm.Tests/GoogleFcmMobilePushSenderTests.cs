using System.Net;
using System.Text.Json;
using Granit.Notifications.GoogleFcm.Internal;
using Granit.Notifications.GoogleFcm.Options;
using Granit.Notifications.MobilePush;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.GoogleFcm.Tests;

public sealed class GoogleFcmMobilePushSenderTests
{
    [Fact]
    public async Task SendAsync_SuccessfulDelivery_DoesNotThrow()
    {
        GoogleFcmMobilePushSender sender = CreateSender(new DelegatingHandlerStub(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        MobilePushMessage message = BuildMessage("token-1");

        await Should.NotThrowAsync(() => sender.SendAsync(message, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendAsync_UnregisteredToken_PublishesInvalidationEvent()
    {
        IMobilePushEventPublisher eventPublisher = Substitute.For<IMobilePushEventPublisher>();
        GoogleFcmMobilePushSender sender = CreateSender(
            new DelegatingHandlerStub(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"error\":{\"code\":404,\"message\":\"UNREGISTERED\"}}"),
            }),
            eventPublisher);

        MobilePushMessage message = BuildMessage("expired-token");
        await sender.SendAsync(message, TestContext.Current.CancellationToken);

        await eventPublisher.Received(1).PublishTokenInvalidatedAsync(
            Arg.Is<MobilePushTokenInvalidated>(e => e.DeviceToken == "expired-token"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_ServerError_ThrowsAggregateException()
    {
        GoogleFcmMobilePushSender sender = CreateSender(
            new DelegatingHandlerStub(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("{\"error\":\"internal\"}"),
            }));

        MobilePushMessage message = BuildMessage("token-1");
        await Should.ThrowAsync<AggregateException>(() => sender.SendAsync(message, TestContext.Current.CancellationToken));
    }

    private static GoogleFcmMobilePushSender CreateSender(DelegatingHandler handler, IMobilePushEventPublisher? eventPublisher = null)
    {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        HttpClient client = new(handler) { BaseAddress = new Uri("https://fcm.googleapis.com/") };
        factory.CreateClient("GoogleFcmPush").Returns(client);

        IOptionsMonitor<GoogleFcmOptions> monitor = Substitute.For<IOptionsMonitor<GoogleFcmOptions>>();
        monitor.CurrentValue.Returns(new GoogleFcmOptions { ProjectId = "test-project", ServiceAccountJson = "{}" });
        return new GoogleFcmMobilePushSender(factory, monitor, eventPublisher ?? Substitute.For<IMobilePushEventPublisher>(), NullLogger<GoogleFcmMobilePushSender>.Instance);
    }

    private static MobilePushMessage BuildMessage(params string[] tokens) => new()
    {
        DeviceTokens = tokens,
        Title = "Test",
        Body = "Test notification",
        Data = JsonSerializer.SerializeToElement(new { key = "value" }),
    };

    private sealed class DelegatingHandlerStub(Func<HttpRequestMessage, HttpResponseMessage> handler) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }
}
