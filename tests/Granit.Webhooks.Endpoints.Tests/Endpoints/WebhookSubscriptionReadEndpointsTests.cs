using Granit.Webhooks.Domain;
using Granit.Webhooks.Endpoints.Dtos;
using Granit.Webhooks.Endpoints.Endpoints;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Endpoints.Tests.Endpoints;

public sealed class WebhookSubscriptionReadEndpointsTests
{
    [Fact]
    public void MapToResponse_PropagatesSigningSecretHint()
    {
        const string Hint = "whsec_b46a****************5182";
        var subscription = WebhookSubscription.Create(
            id: Guid.NewGuid(),
            targetUrl: "https://example.com/hook",
            eventType: "test.event",
            signingKeyId: Guid.NewGuid(),
            protectedSecret: "protected-secret",
            createdAt: DateTimeOffset.UtcNow,
            tenantId: null,
            signingSecretHint: Hint);

        WebhookSubscriptionResponse response = WebhookSubscriptionReadEndpoints.MapToResponse(subscription);

        response.SigningSecretHint.ShouldBe(Hint);
    }

    [Fact]
    public void MapToResponse_NullHint_IsForwardedAsNull()
    {
        var subscription = WebhookSubscription.Create(
            id: Guid.NewGuid(),
            targetUrl: "https://example.com/hook",
            eventType: "test.event",
            signingKeyId: Guid.NewGuid(),
            protectedSecret: "protected-secret",
            createdAt: DateTimeOffset.UtcNow);

        WebhookSubscriptionResponse response = WebhookSubscriptionReadEndpoints.MapToResponse(subscription);

        response.SigningSecretHint.ShouldBeNull();
    }
}
