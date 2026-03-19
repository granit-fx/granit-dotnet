// =============================================================================
// Tests - InMemoryWebhookSubscriptionStore
// =============================================================================
// Verifies filtering by eventType, tenantId, Status = Active; global subscriptions
// (TenantId = null); deactivation.
// =============================================================================

using Granit.Timing;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Tests;

public sealed class InMemoryWebhookSubscriptionStoreTests
{
    private readonly InMemoryWebhookSubscriptionStore _store;

    public InMemoryWebhookSubscriptionStoreTests()
    {
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(_ => DateTimeOffset.UtcNow);
        _store = new InMemoryWebhookSubscriptionStore(clock);
    }

    [Fact]
    public async Task GetActiveSubscriptionsAsync_ReturnsOnlyActiveMatchingSubscriptions()
    {
        var tenantId = Guid.NewGuid();
        _store.Add(BuildSubscription("test.event", tenantId, WebhookSubscriptionStatus.Active));
        _store.Add(BuildSubscription("test.event", tenantId, WebhookSubscriptionStatus.Suspended));
        _store.Add(BuildSubscription("other.event", tenantId, WebhookSubscriptionStatus.Active));

        IReadOnlyList<WebhookSubscription> result =
            await _store.GetActiveSubscriptionsAsync("test.event", tenantId, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetActiveSubscriptionsAsync_IncludesGlobalSubscriptions()
    {
        var tenantId = Guid.NewGuid();
        _store.Add(BuildSubscription("test.event", tenantId: null, WebhookSubscriptionStatus.Active)); // global
        _store.Add(BuildSubscription("test.event", tenantId, WebhookSubscriptionStatus.Active));       // tenant-specific

        IReadOnlyList<WebhookSubscription> result =
            await _store.GetActiveSubscriptionsAsync("test.event", tenantId, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetActiveSubscriptionsAsync_ExcludesOtherTenantsSubscriptions()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        _store.Add(BuildSubscription("test.event", tenantA, WebhookSubscriptionStatus.Active));

        IReadOnlyList<WebhookSubscription> result =
            await _store.GetActiveSubscriptionsAsync("test.event", tenantB, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetActiveSubscriptionsAsync_NoSubscribers_ReturnsEmpty()
    {
        IReadOnlyList<WebhookSubscription> result =
            await _store.GetActiveSubscriptionsAsync("unknown.event", Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsSubscription_WhenExists()
    {
        WebhookSubscription sub = BuildSubscription("test.event", Guid.NewGuid(), WebhookSubscriptionStatus.Active);
        _store.Add(sub);

        WebhookSubscription? found = await _store.FindByIdAsync(sub.Id, TestContext.Current.CancellationToken);

        found.ShouldNotBeNull();
        found!.Id.ShouldBe(sub.Id);
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsNull_WhenNotFound()
    {
        WebhookSubscription? found = await _store.FindByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        found.ShouldBeNull();
    }

    [Fact]
    public async Task DeactivateAsync_SetsStatusToDeactivated()
    {
        WebhookSubscription sub = BuildSubscription("test.event", Guid.NewGuid(), WebhookSubscriptionStatus.Active);
        _store.Add(sub);

        await _store.DeactivateAsync(sub.Id, "test reason", TestContext.Current.CancellationToken);

        WebhookSubscription? updated = await _store.FindByIdAsync(sub.Id, TestContext.Current.CancellationToken);
        updated!.Status.ShouldBe(WebhookSubscriptionStatus.Deactivated);
        updated.DeactivationReason.ShouldBe("test reason");
    }

    [Fact]
    public async Task DeactivateAsync_UnknownId_DoesNotThrow()
    {
        Func<Task> act = () => _store.DeactivateAsync(Guid.NewGuid(), "reason", TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    // -------------------------------------------------------------------------
    // SuspendAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SuspendAsync_SetsStatusToSuspended()
    {
        WebhookSubscription sub = BuildSubscription("test.event", Guid.NewGuid(), WebhookSubscriptionStatus.Active);
        _store.Add(sub);

        await _store.SuspendAsync(sub.Id, "too many failures", TestContext.Current.CancellationToken);

        WebhookSubscription? updated = await _store.FindByIdAsync(sub.Id, TestContext.Current.CancellationToken);
        updated!.Status.ShouldBe(WebhookSubscriptionStatus.Suspended);
        updated.DeactivationReason.ShouldBe("too many failures");
        updated.SuspendedAt.ShouldNotBeNull();
        updated.SuspendedBy.ShouldBe("system");
    }

    [Fact]
    public async Task SuspendAsync_UnknownId_DoesNotThrow()
    {
        Func<Task> act = () => _store.SuspendAsync(Guid.NewGuid(), "reason", TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task SuspendAsync_SuspendedSubscription_NotReturnedByGetActive()
    {
        var tenantId = Guid.NewGuid();
        WebhookSubscription sub = BuildSubscription("test.event", tenantId, WebhookSubscriptionStatus.Active);
        _store.Add(sub);

        await _store.SuspendAsync(sub.Id, "suspended", TestContext.Current.CancellationToken);

        IReadOnlyList<WebhookSubscription> result =
            await _store.GetActiveSubscriptionsAsync("test.event", tenantId, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static WebhookSubscription BuildSubscription(
        string eventType,
        Guid? tenantId,
        WebhookSubscriptionStatus status)
    {
        var sub = WebhookSubscription.Create(Guid.NewGuid(), "https://example.com/webhook", eventType, "protected-secret", tenantId);

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
