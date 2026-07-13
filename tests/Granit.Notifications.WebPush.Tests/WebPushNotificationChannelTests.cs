// =============================================================================
// Tests - WebPushNotificationChannel
// =============================================================================
// Verifies the Web Push channel implementation: no-subscription short circuit,
// name property, store interaction, payload construction, multi-subscription
// delivery, and expired subscription cleanup (HTTP 410 Gone).
// PushServiceClient has no virtual methods so full send tests use a mock
// HttpMessageHandler to avoid real HTTP calls.
// =============================================================================

using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Granit.Notifications.WebPush.Internal;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.WebPush.Tests;

public sealed class WebPushNotificationChannelTests
{
    // Pre-generated P-256 subscription key pair for push encryption (test-only).
    private static readonly string TestP256dh;
    private static readonly string TestAuth;

    private readonly IWebPushSubscriptionReader _subscriptionReader = Substitute.For<IWebPushSubscriptionReader>();
    private readonly IWebPushSubscriptionWriter _subscriptionWriter = Substitute.For<IWebPushSubscriptionWriter>();
    private readonly ILogger<WebPushNotificationChannel> _logger = Substitute.For<ILogger<WebPushNotificationChannel>>();

    static WebPushNotificationChannelTests()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ECParameters parameters = ecdsa.ExportParameters(includePrivateParameters: false);
        // Uncompressed P-256 public key: 0x04 || X || Y
        byte[] uncompressedKey = [0x04, .. parameters.Q.X!, .. parameters.Q.Y!];
        TestP256dh = Base64UrlEncode(uncompressedKey);

        byte[] authSecret = new byte[16];
        RandomNumberGenerator.Fill(authSecret);
        TestAuth = Base64UrlEncode(authSecret);
    }

    [Fact]
    public void Name_ReturnsWebPush()
    {
        WebPushNotificationChannel channel = BuildChannel();

        channel.Name.ShouldBe(NotificationChannels.WebPush);
    }

    [Fact]
    public async Task SendAsync_NoSubscriptions_DoesNotThrow()
    {
        _subscriptionReader.GetSubscriptionsAsync(
            Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<WebPushSubscriptionInfo>>([]));
        WebPushNotificationChannel channel = BuildChannel();
        NotificationDeliveryContext context = BuildContext();

        Func<Task> act = () => channel.SendAsync(context, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task SendAsync_NoSubscriptions_QueriesStoreWithCorrectUser()
    {
        _subscriptionReader.GetSubscriptionsAsync(
            Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<WebPushSubscriptionInfo>>([]));
        WebPushNotificationChannel channel = BuildChannel();
        NotificationDeliveryContext context = BuildContext();

        await channel.SendAsync(context, TestContext.Current.CancellationToken);

        await _subscriptionReader.Received(1).GetSubscriptionsAsync(
            context.RecipientUserId, context.TenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithSubscription_SendsEncryptedContent()
    {
        MockHttpMessageHandler handler = new();
        WebPushNotificationChannel channel = BuildChannel(handler);
        NotificationDeliveryContext context = BuildContext();
        SetupSubscriptions(context.RecipientUserId, context.TenantId,
            [BuildSubscription("https://push.example.com/sub1")]);

        await channel.SendAsync(context, TestContext.Current.CancellationToken);

        handler.Requests.ShouldHaveSingleItem();
        // The body is AES-128-GCM encrypted, so we verify content was sent (non-empty).
        handler.RequestBodies.ShouldHaveSingleItem().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task SendAsync_WithSubscription_SendsPostRequest()
    {
        MockHttpMessageHandler handler = new();
        WebPushNotificationChannel channel = BuildChannel(handler);
        NotificationDeliveryContext context = BuildContext();
        SetupSubscriptions(context.RecipientUserId, context.TenantId,
            [BuildSubscription("https://push.example.com/sub1")]);

        await channel.SendAsync(context, TestContext.Current.CancellationToken);

        handler.Requests.ShouldHaveSingleItem().Method.ShouldBe(HttpMethod.Post);
    }

    [Fact]
    public async Task SendAsync_WithMultipleSubscriptions_SendsToAll()
    {
        MockHttpMessageHandler handler = new();
        WebPushNotificationChannel channel = BuildChannel(handler);
        NotificationDeliveryContext context = BuildContext();
        List<WebPushSubscriptionInfo> subscriptions =
        [
            BuildSubscription("https://push.example.com/sub1"),
            BuildSubscription("https://push.example.com/sub2"),
            BuildSubscription("https://push.example.com/sub3"),
        ];
        SetupSubscriptions(context.RecipientUserId, context.TenantId, subscriptions);

        await channel.SendAsync(context, TestContext.Current.CancellationToken);

        handler.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task SendAsync_WithSubscription_SendsRequestToCorrectEndpoint()
    {
        MockHttpMessageHandler handler = new();
        WebPushNotificationChannel channel = BuildChannel(handler);
        NotificationDeliveryContext context = BuildContext();
        SetupSubscriptions(context.RecipientUserId, context.TenantId,
            [BuildSubscription("https://push.example.com/unique-endpoint")]);

        await channel.SendAsync(context, TestContext.Current.CancellationToken);

        handler.Requests.ShouldHaveSingleItem();
        handler.Requests[0].RequestUri!.ToString().ShouldContain("unique-endpoint");
    }

    [Fact]
    public async Task SendAsync_GoneResponse_RemovesExpiredSubscription()
    {
        MockHttpMessageHandler handler = new() { ResponseStatusCode = HttpStatusCode.Gone };
        WebPushNotificationChannel channel = BuildChannel(handler);
        NotificationDeliveryContext context = BuildContext();
        const string expiredEndpoint = "https://push.example.com/expired";
        SetupSubscriptions(context.RecipientUserId, context.TenantId,
            [BuildSubscription(expiredEndpoint)]);

        await channel.SendAsync(context, TestContext.Current.CancellationToken);

        await _subscriptionWriter.Received(1).RemoveSubscriptionAsync(
            context.RecipientUserId, expiredEndpoint, context.TenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_GoneResponse_WithTenantId_RemovesWithCorrectTenantId()
    {
        MockHttpMessageHandler handler = new() { ResponseStatusCode = HttpStatusCode.Gone };
        WebPushNotificationChannel channel = BuildChannel(handler);
        var tenantId = Guid.NewGuid();
        NotificationDeliveryContext context = new()
        {
            DeliveryId = Guid.NewGuid(),
            NotificationId = Guid.NewGuid(),
            NotificationTypeName = "test.notification",
            RecipientUserId = "user-1",
            Severity = NotificationSeverity.Info,
            Data = JsonSerializer.SerializeToElement(new { key = "value" }),
            OccurredAt = DateTimeOffset.UtcNow,
            TenantId = tenantId,
        };
        const string expiredEndpoint = "https://push.example.com/expired-tenant";
        SetupSubscriptions(context.RecipientUserId, tenantId,
            [BuildSubscription(expiredEndpoint)]);

        await channel.SendAsync(context, TestContext.Current.CancellationToken);

        await _subscriptionWriter.Received(1).RemoveSubscriptionAsync(
            context.RecipientUserId, expiredEndpoint, tenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_GoneResponseForOneOfMultiple_RemovesOnlyExpiredAndContinues()
    {
        // First request succeeds, second returns Gone, third succeeds.
        // Use a handler that returns different statuses per request.
        SequentialMockHttpMessageHandler handler = new([
            HttpStatusCode.Created,
            HttpStatusCode.Gone,
            HttpStatusCode.Created,
        ]);
        WebPushNotificationChannel channel = BuildChannel(handler);
        NotificationDeliveryContext context = BuildContext();
        List<WebPushSubscriptionInfo> subscriptions =
        [
            BuildSubscription("https://push.example.com/active1"),
            BuildSubscription("https://push.example.com/expired"),
            BuildSubscription("https://push.example.com/active2"),
        ];
        SetupSubscriptions(context.RecipientUserId, context.TenantId, subscriptions);

        await channel.SendAsync(context, TestContext.Current.CancellationToken);

        // All three subscriptions should have been attempted.
        handler.Requests.Count.ShouldBe(3);
        // Only the expired endpoint should be removed.
        await _subscriptionWriter.Received(1).RemoveSubscriptionAsync(
            context.RecipientUserId, "https://push.example.com/expired", context.TenantId, Arg.Any<CancellationToken>());
        await _subscriptionWriter.DidNotReceive().RemoveSubscriptionAsync(
            Arg.Any<string>(), "https://push.example.com/active1", Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _subscriptionWriter.DidNotReceive().RemoveSubscriptionAsync(
            Arg.Any<string>(), "https://push.example.com/active2", Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithTenantId_QueriesStoreWithTenantId()
    {
        MockHttpMessageHandler handler = new();
        WebPushNotificationChannel channel = BuildChannel(handler);
        var tenantId = Guid.NewGuid();
        NotificationDeliveryContext context = new()
        {
            DeliveryId = Guid.NewGuid(),
            NotificationId = Guid.NewGuid(),
            NotificationTypeName = "test.notification",
            RecipientUserId = "user-1",
            Severity = NotificationSeverity.Info,
            Data = JsonSerializer.SerializeToElement(new { key = "value" }),
            OccurredAt = DateTimeOffset.UtcNow,
            TenantId = tenantId,
        };
        SetupSubscriptions("user-1", tenantId,
            [BuildSubscription("https://push.example.com/sub1")]);

        await channel.SendAsync(context, TestContext.Current.CancellationToken);

        await _subscriptionReader.Received(1).GetSubscriptionsAsync(
            "user-1", tenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithSubscription_IncludesVapidAuthorizationHeader()
    {
        MockHttpMessageHandler handler = new();
        WebPushNotificationChannel channel = BuildChannel(handler);
        NotificationDeliveryContext context = BuildContext();
        SetupSubscriptions(context.RecipientUserId, context.TenantId,
            [BuildSubscription("https://push.example.com/sub1")]);

        await channel.SendAsync(context, TestContext.Current.CancellationToken);

        HttpRequestMessage request = handler.Requests[0];
        // VAPID authentication adds an Authorization header (vapid scheme).
        request.Headers.Authorization.ShouldNotBeNull();
    }

    [Fact]
    public async Task SendAsync_NoSubscriptions_DoesNotSendAnyPushMessage()
    {
        MockHttpMessageHandler handler = new();
        WebPushNotificationChannel channel = BuildChannel(handler);
        _subscriptionReader.GetSubscriptionsAsync(
            Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<WebPushSubscriptionInfo>>([]));
        NotificationDeliveryContext context = BuildContext();

        await channel.SendAsync(context, TestContext.Current.CancellationToken);

        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_ServerError_ThrowsAggregateExceptionAfterAllAttempts()
    {
        // First succeeds, second returns 500 (non-retriable by the channel), third succeeds.
        SequentialMockHttpMessageHandler handler = new([
            HttpStatusCode.Created,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.Created,
        ]);
        WebPushNotificationChannel channel = BuildChannel(handler);
        NotificationDeliveryContext context = BuildContext();
        List<WebPushSubscriptionInfo> subscriptions =
        [
            BuildSubscription("https://push.example.com/active1"),
            BuildSubscription("https://push.example.com/failing"),
            BuildSubscription("https://push.example.com/active2"),
        ];
        SetupSubscriptions(context.RecipientUserId, context.TenantId, subscriptions);

        AggregateException ex = await Should.ThrowAsync<AggregateException>(
            () => channel.SendAsync(context, TestContext.Current.CancellationToken));

        // All three subscriptions should have been attempted despite the 500 error.
        handler.Requests.Count.ShouldBe(3);
        ex.InnerExceptions.Count.ShouldBe(1);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private WebPushNotificationChannel BuildChannel() =>
        BuildChannel(new MockHttpMessageHandler());

    private WebPushNotificationChannel BuildChannel(HttpMessageHandler handler)
    {
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://push.example.com") };
        PushServiceClient pushServiceClient = new(httpClient)
        {
            DefaultAuthentication = CreateTestVapidAuthentication(),
        };
        return new WebPushNotificationChannel(pushServiceClient, _subscriptionReader, _subscriptionWriter, _logger);
    }

    private static VapidAuthentication CreateTestVapidAuthentication()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ECParameters parameters = ecdsa.ExportParameters(includePrivateParameters: true);
        string publicKey = Base64UrlEncode([0x04, .. parameters.Q.X!, .. parameters.Q.Y!]);
        string privateKey = Base64UrlEncode(parameters.D!);
        return new VapidAuthentication(publicKey, privateKey) { Subject = "mailto:test@test.com" };
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private void SetupSubscriptions(string userId, Guid? tenantId, List<WebPushSubscriptionInfo> subscriptions) =>
        _subscriptionReader.GetSubscriptionsAsync(userId, tenantId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<WebPushSubscriptionInfo>>(subscriptions));

    private static WebPushSubscriptionInfo BuildSubscription(string endpoint) => new()
    {
        Endpoint = endpoint,
        P256dh = TestP256dh,
        Auth = TestAuth,
    };

    private static NotificationDeliveryContext BuildContext() => new()
    {
        DeliveryId = Guid.NewGuid(),
        NotificationId = Guid.NewGuid(),
        NotificationTypeName = "test.notification",
        RecipientUserId = "user-1",
        Severity = NotificationSeverity.Info,
        Data = JsonSerializer.SerializeToElement(new { key = "value" }),
        OccurredAt = DateTimeOffset.UtcNow,
    };
}
