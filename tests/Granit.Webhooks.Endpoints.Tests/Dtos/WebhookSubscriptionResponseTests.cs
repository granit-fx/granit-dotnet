using Granit.Webhooks.Domain;
using Granit.Webhooks.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Endpoints.Tests.Dtos;

public sealed class WebhookSubscriptionResponseTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var id = Guid.NewGuid();
        string targetUrl = "https://example.com/webhook";
        string eventType = "order.created";
        WebhookSubscriptionStatus status = WebhookSubscriptionStatus.Active;
        int failureCount = 3;
        DateTimeOffset lastSuccessAt = DateTimeOffset.UtcNow.AddHours(-1);
        DateTimeOffset createdAt = DateTimeOffset.UtcNow.AddDays(-7);
        DateTimeOffset modifiedAt = DateTimeOffset.UtcNow;

        var response = new WebhookSubscriptionResponse(
            id,
            targetUrl,
            eventType,
            status,
            failureCount,
            lastSuccessAt,
            createdAt,
            modifiedAt);

        response.Id.ShouldBe(id);
        response.TargetUrl.ShouldBe(targetUrl);
        response.EventType.ShouldBe(eventType);
        response.Status.ShouldBe(WebhookSubscriptionStatus.Active);
        response.ConsecutiveFailureCount.ShouldBe(3);
        response.LastSuccessAt.ShouldBe(lastSuccessAt);
        response.CreatedAt.ShouldBe(createdAt);
        response.ModifiedAt.ShouldBe(modifiedAt);
    }

    [Fact]
    public void Constructor_NullableFields_CanBeNull()
    {
        var response = new WebhookSubscriptionResponse(
            Guid.NewGuid(),
            "https://example.com/webhook",
            "order.created",
            WebhookSubscriptionStatus.Suspended,
            0,
            null,
            DateTimeOffset.UtcNow,
            null);

        response.LastSuccessAt.ShouldBeNull();
        response.ModifiedAt.ShouldBeNull();
    }
}
