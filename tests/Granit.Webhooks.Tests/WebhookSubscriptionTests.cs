// =============================================================================
// Tests - WebhookSubscription domain events
// =============================================================================
// Verifies IDomainEventSource implementation: Suspend and Deactivate emit
// the correct domain events, ClearDomainEvents resets the collection.
// =============================================================================

using Granit.Events;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Events;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Tests;

public sealed class WebhookSubscriptionTests
{
    [Fact]
    public void Suspend_ShouldEmitWebhookSubscriptionSuspendedEventEvent()
    {
        WebhookSubscription subscription = BuildSubscription();
        subscription.ClearDomainEvents();

        subscription.Suspend(DateTimeOffset.UtcNow, "system", "HTTP 401");

        subscription.Status.ShouldBe(WebhookSubscriptionStatus.Suspended);
        subscription.DeactivationReason.ShouldBe("HTTP 401");
        subscription.SuspendedBy.ShouldBe("system");

        IDomainEvent domainEvent = subscription.DomainEvents.ShouldHaveSingleItem();
        WebhookSubscriptionSuspendedEvent suspended = domainEvent.ShouldBeOfType<WebhookSubscriptionSuspendedEvent>();
        suspended.SubscriptionId.ShouldBe(subscription.Id);
        suspended.Reason.ShouldBe("HTTP 401");
    }

    [Fact]
    public void Deactivate_ShouldEmitWebhookSubscriptionDeactivatedEventEvent()
    {
        WebhookSubscription subscription = BuildSubscription();
        subscription.ClearDomainEvents();

        subscription.Deactivate("Admin request");

        subscription.Status.ShouldBe(WebhookSubscriptionStatus.Deactivated);
        subscription.DeactivationReason.ShouldBe("Admin request");

        IDomainEvent domainEvent = subscription.DomainEvents.ShouldHaveSingleItem();
        WebhookSubscriptionDeactivatedEvent deactivated = domainEvent.ShouldBeOfType<WebhookSubscriptionDeactivatedEvent>();
        deactivated.SubscriptionId.ShouldBe(subscription.Id);
        deactivated.Reason.ShouldBe("Admin request");
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemoveAllCollectedEvents()
    {
        WebhookSubscription subscription = BuildSubscription();
        subscription.Suspend(DateTimeOffset.UtcNow, "system", "HTTP 404");

        subscription.ClearDomainEvents();

        subscription.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void MultipleTransitions_ShouldAccumulateEvents()
    {
        WebhookSubscription subscription = BuildSubscription();
        subscription.ClearDomainEvents();

        subscription.Suspend(DateTimeOffset.UtcNow, "system", "HTTP 401");
        subscription.Deactivate("Permanently removed");

        subscription.DomainEvents.Count.ShouldBe(2);
        subscription.DomainEvents.First().ShouldBeOfType<WebhookSubscriptionSuspendedEvent>();
        subscription.DomainEvents.Last().ShouldBeOfType<WebhookSubscriptionDeactivatedEvent>();
    }

    [Fact]
    public void Create_ShouldEmitWebhookSubscriptionCreatedEvent()
    {
        WebhookSubscription subscription = BuildSubscription();

        IDomainEvent domainEvent = subscription.DomainEvents.ShouldHaveSingleItem();
        WebhookSubscriptionCreatedEvent created = domainEvent.ShouldBeOfType<WebhookSubscriptionCreatedEvent>();
        created.SubscriptionId.ShouldBe(subscription.Id);
        created.EventType.ShouldBe("test.event");
        created.TargetUrl.ShouldBe("https://example.com/webhook");
    }

    [Fact]
    public void RecordSuccess_ShouldEmitWebhookDeliverySucceededEvent()
    {
        WebhookSubscription subscription = BuildSubscription();
        subscription.ClearDomainEvents();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        subscription.RecordSuccess(now);

        IDomainEvent domainEvent = subscription.DomainEvents.ShouldHaveSingleItem();
        WebhookDeliverySucceededEvent succeeded = domainEvent.ShouldBeOfType<WebhookDeliverySucceededEvent>();
        succeeded.SubscriptionId.ShouldBe(subscription.Id);
        succeeded.DeliveredAt.ShouldBe(now);
    }

    [Fact]
    public void RecordFailure_BelowThreshold_ShouldNotEmitIntegrationEvent()
    {
        WebhookSubscription subscription = BuildSubscription();

        for (int i = 0; i < 4; i++)
        {
            subscription.RecordFailure();
        }

        subscription.ConsecutiveFailureCount.ShouldBe(4);
        subscription.IntegrationEvents.ShouldBeEmpty();
    }

    [Fact]
    public void RecordFailure_AtThreshold_ShouldEmitWebhookDeliveryFailureThresholdExceededEto()
    {
        WebhookSubscription subscription = BuildSubscription();

        for (int i = 0; i < 5; i++)
        {
            subscription.RecordFailure();
        }

        subscription.ConsecutiveFailureCount.ShouldBe(5);

        IIntegrationEvent integrationEvent = subscription.IntegrationEvents.ShouldHaveSingleItem();
        WebhookDeliveryFailureThresholdExceededEto eto = integrationEvent.ShouldBeOfType<WebhookDeliveryFailureThresholdExceededEto>();
        eto.SubscriptionId.ShouldBe(subscription.Id);
        eto.TargetUrl.ShouldBe("https://example.com/webhook");
        eto.ConsecutiveFailureCount.ShouldBe(5);
    }

    [Fact]
    public void RecordFailure_AboveThreshold_ShouldEmitOnEachSubsequentFailure()
    {
        WebhookSubscription subscription = BuildSubscription();

        for (int i = 0; i < 7; i++)
        {
            subscription.RecordFailure();
        }

        subscription.IntegrationEvents.Count.ShouldBe(3); // failures 5, 6, 7
    }

    private static WebhookSubscription BuildSubscription() =>
        WebhookSubscription.Create(
            Guid.NewGuid(), "https://example.com/webhook", "test.event",
            signingKeyId: Guid.NewGuid(), protectedSecret: "protected-secret", createdAt: DateTimeOffset.UtcNow);
}
