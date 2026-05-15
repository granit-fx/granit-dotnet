// =============================================================================
// Tests - RetryWebhookHandler
// =============================================================================
// Verifies retry validation: not found, success rejection, deactivated conflict,
// suspended subscription allowed, and successful command creation.
// =============================================================================

using System.Text.Json;
using Granit.Guids;
using Granit.Timing;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Handlers;
using Granit.Webhooks.Messages;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Tests;

public sealed class RetryWebhookHandlerTests
{
    private readonly IWebhookDeliveryReader _deliveryReader = Substitute.For<IWebhookDeliveryReader>();
    private readonly IWebhookSubscriptionReader _subscriptionReader = Substitute.For<IWebhookSubscriptionReader>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly RetryWebhookHandler _handler;

    public RetryWebhookHandlerTests()
    {
        _guidGenerator.Create().Returns(_ => Guid.NewGuid());
        _clock.Now.Returns(new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero));
        _handler = new RetryWebhookHandler(_deliveryReader, _subscriptionReader, _guidGenerator, _clock);
    }

    // -------------------------------------------------------------------------
    // Not found
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_DeliveryNotFound_ReturnsNotFound()
    {
        var deliveryId = Guid.NewGuid();
        _deliveryReader.FindByDeliveryIdAsync(deliveryId, Arg.Any<CancellationToken>())
            .Returns((WebhookDeliveryAttempt?)null);

        RetryWebhookResult result = await _handler.HandleAsync(deliveryId, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorKind.ShouldBe(RetryWebhookErrorKind.NotFound);
        result.Error!.ShouldContain(deliveryId.ToString());
    }

    // -------------------------------------------------------------------------
    // Success rejection (400)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_SuccessfulAttempt_ReturnsInvalidRequest()
    {
        WebhookDeliveryAttempt attempt = BuildAttempt(isSuccess: true);
        _deliveryReader.FindByDeliveryIdAsync(attempt.DeliveryId, Arg.Any<CancellationToken>())
            .Returns(attempt);

        RetryWebhookResult result = await _handler.HandleAsync(attempt.DeliveryId, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorKind.ShouldBe(RetryWebhookErrorKind.InvalidRequest);
    }

    // -------------------------------------------------------------------------
    // Subscription not found
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_SubscriptionNotFound_ReturnsNotFound()
    {
        WebhookDeliveryAttempt attempt = BuildAttempt(isSuccess: false);
        _deliveryReader.FindByDeliveryIdAsync(attempt.DeliveryId, Arg.Any<CancellationToken>())
            .Returns(attempt);
        _subscriptionReader.FindByIdAsync(attempt.SubscriptionId, Arg.Any<CancellationToken>())
            .Returns((WebhookSubscription?)null);

        RetryWebhookResult result = await _handler.HandleAsync(attempt.DeliveryId, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorKind.ShouldBe(RetryWebhookErrorKind.NotFound);
    }

    // -------------------------------------------------------------------------
    // Deactivated subscription (409)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_DeactivatedSubscription_ReturnsConflict()
    {
        WebhookDeliveryAttempt attempt = BuildAttempt(isSuccess: false);
        WebhookSubscription subscription = BuildSubscription(attempt.SubscriptionId, WebhookSubscriptionStatus.Deactivated);

        _deliveryReader.FindByDeliveryIdAsync(attempt.DeliveryId, Arg.Any<CancellationToken>())
            .Returns(attempt);
        _subscriptionReader.FindByIdAsync(attempt.SubscriptionId, Arg.Any<CancellationToken>())
            .Returns(subscription);

        RetryWebhookResult result = await _handler.HandleAsync(attempt.DeliveryId, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorKind.ShouldBe(RetryWebhookErrorKind.Conflict);
    }

    // -------------------------------------------------------------------------
    // Suspended subscription — allowed
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_SuspendedSubscription_ReturnsSuccess()
    {
        WebhookDeliveryAttempt attempt = BuildAttempt(isSuccess: false);
        WebhookSubscription subscription = BuildSubscription(attempt.SubscriptionId, WebhookSubscriptionStatus.Suspended);

        _deliveryReader.FindByDeliveryIdAsync(attempt.DeliveryId, Arg.Any<CancellationToken>())
            .Returns(attempt);
        _subscriptionReader.FindByIdAsync(attempt.SubscriptionId, Arg.Any<CancellationToken>())
            .Returns(subscription);

        RetryWebhookResult result = await _handler.HandleAsync(attempt.DeliveryId, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Command.ShouldNotBeNull();
    }

    // -------------------------------------------------------------------------
    // Active subscription — success path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_ActiveSubscription_ReturnsCommandWithCorrectFields()
    {
        WebhookDeliveryAttempt attempt = BuildAttempt(isSuccess: false);
        WebhookSubscription subscription = BuildSubscription(attempt.SubscriptionId, WebhookSubscriptionStatus.Active);

        _deliveryReader.FindByDeliveryIdAsync(attempt.DeliveryId, Arg.Any<CancellationToken>())
            .Returns(attempt);
        _subscriptionReader.FindByIdAsync(attempt.SubscriptionId, Arg.Any<CancellationToken>())
            .Returns(subscription);

        RetryWebhookResult result = await _handler.HandleAsync(attempt.DeliveryId, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        SendWebhookCommand command = result.Command!;
        command.SubscriptionId.ShouldBe(subscription.Id);
        command.TargetUrl.ShouldBe(subscription.TargetUrl);
        command.Envelope.EventType.ShouldBe(attempt.EventType);
        command.Envelope.TenantId.ShouldBe(attempt.TenantId);
        command.DeliveryId.ShouldNotBe(attempt.DeliveryId);
    }

    [Fact]
    public async Task HandleAsync_WithStoredPayload_ExtractsDataField()
    {
        string payloadJson = JsonSerializer.Serialize(new WebhookEnvelope
        {
            EventId = Guid.NewGuid(),
            EventType = "test.event",
            TenantId = null,
            Timestamp = DateTimeOffset.UtcNow,
            ApiVersion = "2025-01-01",
            Data = JsonSerializer.SerializeToElement(new { orderId = 42 }),
        });

        WebhookDeliveryAttempt attempt = BuildAttempt(isSuccess: false, payload: payloadJson);
        WebhookSubscription subscription = BuildSubscription(attempt.SubscriptionId, WebhookSubscriptionStatus.Active);

        _deliveryReader.FindByDeliveryIdAsync(attempt.DeliveryId, Arg.Any<CancellationToken>())
            .Returns(attempt);
        _subscriptionReader.FindByIdAsync(attempt.SubscriptionId, Arg.Any<CancellationToken>())
            .Returns(subscription);

        RetryWebhookResult result = await _handler.HandleAsync(attempt.DeliveryId, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Command!.Envelope.Data.GetProperty("orderId").GetInt32().ShouldBe(42);
    }

    [Fact]
    public async Task HandleAsync_WithoutStoredPayload_ReturnsEmptyData()
    {
        WebhookDeliveryAttempt attempt = BuildAttempt(isSuccess: false, payload: null);
        WebhookSubscription subscription = BuildSubscription(attempt.SubscriptionId, WebhookSubscriptionStatus.Active);

        _deliveryReader.FindByDeliveryIdAsync(attempt.DeliveryId, Arg.Any<CancellationToken>())
            .Returns(attempt);
        _subscriptionReader.FindByIdAsync(attempt.SubscriptionId, Arg.Any<CancellationToken>())
            .Returns(subscription);

        RetryWebhookResult result = await _handler.HandleAsync(attempt.DeliveryId, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Command!.Envelope.Data.ValueKind.ShouldBe(JsonValueKind.Object);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static WebhookDeliveryAttempt BuildAttempt(bool isSuccess, string? payload = null) => new()
    {
        Id = Guid.NewGuid(),
        DeliveryId = Guid.NewGuid(),
        SubscriptionId = Guid.NewGuid(),
        EventType = "test.event",
        TargetUrl = "https://example.com/webhook",
        HttpStatusCode = isSuccess ? 200 : 500,
        PayloadHash = "abc123",
        Payload = payload,
        OccurredAt = DateTimeOffset.UtcNow,
        DurationMs = 150,
        IsSuccess = isSuccess,
    };

    private static WebhookSubscription BuildSubscription(Guid id, WebhookSubscriptionStatus status)
    {
        var sub = WebhookSubscription.Create(
            id, "https://example.com/webhook", "test.event",
            signingKeyId: Guid.NewGuid(), protectedSecret: "test-secret", createdAt: DateTimeOffset.UtcNow);

        switch (status)
        {
            case WebhookSubscriptionStatus.Suspended:
                sub.Suspend(DateTimeOffset.UtcNow, "system", "test suspension");
                sub.ClearDomainEvents();
                break;
            case WebhookSubscriptionStatus.Deactivated:
                sub.Deactivate("test deactivation");
                sub.ClearDomainEvents();
                break;
        }

        return sub;
    }
}
