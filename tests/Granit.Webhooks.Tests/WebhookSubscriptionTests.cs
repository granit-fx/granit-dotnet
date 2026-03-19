// =============================================================================
// Tests - WebhookSubscription domain events
// =============================================================================
// Verifies IDomainEventSource implementation: Suspend and Deactivate emit
// the correct domain events, ClearDomainEvents resets the collection.
// =============================================================================

using Granit.Core.Events;
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

        subscription.Suspend(DateTimeOffset.UtcNow, "system", "HTTP 401");
        subscription.Deactivate("Permanently removed");

        subscription.DomainEvents.Count.ShouldBe(2);
        subscription.DomainEvents.First().ShouldBeOfType<WebhookSubscriptionSuspendedEvent>();
        subscription.DomainEvents.Last().ShouldBeOfType<WebhookSubscriptionDeactivatedEvent>();
    }

    private static WebhookSubscription BuildSubscription() =>
        WebhookSubscription.Create(Guid.NewGuid(), "https://example.com/webhook", "test.event", "protected-secret");
}
