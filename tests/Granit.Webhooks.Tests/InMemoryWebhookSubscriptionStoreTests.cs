// =============================================================================
// Tests - InMemoryWebhookSubscriptionStore
// =============================================================================
// Verifies filtering by eventType, tenantId, Status = Active; global subscriptions
// (TenantId = null); CRUD operations and lifecycle transitions.
// =============================================================================

using Granit.Exceptions;
using Granit.Guids;
using Granit.Timing;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Internal;
using Granit.Webhooks.Options;
using Microsoft.Extensions.Options;
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

        IGuidGenerator guidGenerator = Substitute.For<IGuidGenerator>();
        guidGenerator.Create().Returns(_ => Guid.NewGuid());

        IWebhookSecretProtector secretProtector = Substitute.For<IWebhookSecretProtector>();
#pragma warning disable CA2012 // NSubstitute ValueTask setup — consumed exactly once per call
        secretProtector.ProtectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => new ValueTask<string>($"protected:{ci.ArgAt<string>(0)}"));
#pragma warning restore CA2012

        IOptions<WebhooksOptions> options = Microsoft.Extensions.Options.Options.Create(new WebhooksOptions());

        _store = new InMemoryWebhookSubscriptionStore(clock, guidGenerator, secretProtector, options);
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
    public async Task GetAllAsync_ReturnsAllSubscriptions()
    {
        _store.Add(BuildSubscription("a.event", null, WebhookSubscriptionStatus.Active));
        _store.Add(BuildSubscription("b.event", null, WebhookSubscriptionStatus.Suspended));
        _store.Add(BuildSubscription("c.event", null, WebhookSubscriptionStatus.Deactivated));

        IReadOnlyList<WebhookSubscription> result =
            await _store.GetAllAsync(TestContext.Current.CancellationToken);

        result.Count.ShouldBe(3);
    }

    // -------------------------------------------------------------------------
    // CreateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ReturnsSubscriptionWithPlainSecret()
    {
        WebhookSubscriptionCreatedResult result = await _store.CreateAsync(
            "https://example.com/hook", "test.event", null, TestContext.Current.CancellationToken);

        result.Subscription.ShouldNotBeNull();
        result.Subscription.TargetUrl.Value.ShouldBe("https://example.com/hook");
        result.Subscription.EventType.ShouldBe("test.event");
        result.Subscription.Status.ShouldBe(WebhookSubscriptionStatus.Active);
        result.PlainSecret.ShouldStartWith("whsec_");
    }

    [Fact]
    public async Task CreateAsync_PersistsSigningSecretHint_DerivedFromPlainSecret()
    {
        WebhookSubscriptionCreatedResult result = await _store.CreateAsync(
            "https://example.com/hook", "test.event", null, TestContext.Current.CancellationToken);

        result.Subscription.SigningSecretHint.ShouldNotBeNull();
        result.Subscription.SigningSecretHint.ShouldBe(WebhookSecretHint.From(result.PlainSecret));
        // Never expose the plaintext through the hint.
        result.Subscription.SigningSecretHint.ShouldNotContain(result.PlainSecret[10..30]);
    }

    [Fact]
    public async Task RotateSigningKeyAsync_RefreshesSigningSecretHintOnSubscription()
    {
        WebhookSubscriptionCreatedResult created = await _store.CreateAsync(
            "https://example.com/hook", "test.event", null, TestContext.Current.CancellationToken);
        string initialHint = created.Subscription.SigningSecretHint!;

        WebhookSigningKeyRotatedResult rotated = await ((IWebhookSigningKeyWriter)_store)
            .RotateSigningKeyAsync(created.Subscription.Id, retiredKeyGracePeriod: null, TestContext.Current.CancellationToken);

        WebhookSubscription? updated = await _store.FindByIdAsync(created.Subscription.Id, TestContext.Current.CancellationToken);
        updated!.SigningSecretHint.ShouldNotBeNull();
        updated.SigningSecretHint.ShouldBe(WebhookSecretHint.From(rotated.PlainSecret));
        updated.SigningSecretHint.ShouldNotBe(initialHint);
    }

    // -------------------------------------------------------------------------
    // DeactivateAsync
    // -------------------------------------------------------------------------

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
    public async Task DeactivateAsync_UnknownId_ThrowsEntityNotFoundException()
    {
        Func<Task> act = () => _store.DeactivateAsync(Guid.NewGuid(), "reason", TestContext.Current.CancellationToken);

        await Should.ThrowAsync<EntityNotFoundException>(act);
    }

    [Fact]
    public async Task DeactivateAsync_AlreadyDeactivated_ThrowsConflictException()
    {
        WebhookSubscription sub = BuildSubscription("test.event", null, WebhookSubscriptionStatus.Deactivated);
        _store.Add(sub);

        Func<Task> act = () => _store.DeactivateAsync(sub.Id, "reason", TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ConflictException>(act);
    }

    // -------------------------------------------------------------------------
    // SuspendAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SuspendAsync_SetsStatusToSuspended()
    {
        WebhookSubscription sub = BuildSubscription("test.event", Guid.NewGuid(), WebhookSubscriptionStatus.Active);
        _store.Add(sub);

        await _store.SuspendAsync(sub.Id, "admin-user-id", "too many failures", TestContext.Current.CancellationToken);

        WebhookSubscription? updated = await _store.FindByIdAsync(sub.Id, TestContext.Current.CancellationToken);
        updated!.Status.ShouldBe(WebhookSubscriptionStatus.Suspended);
        updated.DeactivationReason.ShouldBe("too many failures");
        updated.SuspendedAt.ShouldNotBeNull();
        updated.SuspendedBy.ShouldBe("admin-user-id");
    }

    [Fact]
    public async Task SuspendAsync_UnknownId_ThrowsEntityNotFoundException()
    {
        Func<Task> act = () => _store.SuspendAsync(Guid.NewGuid(), "user", "reason", TestContext.Current.CancellationToken);

        await Should.ThrowAsync<EntityNotFoundException>(act);
    }

    [Fact]
    public async Task SuspendAsync_NotActive_ThrowsConflictException()
    {
        WebhookSubscription sub = BuildSubscription("test.event", null, WebhookSubscriptionStatus.Suspended);
        _store.Add(sub);

        Func<Task> act = () => _store.SuspendAsync(sub.Id, "user", "reason", TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ConflictException>(act);
    }

    [Fact]
    public async Task SuspendAsync_SuspendedSubscription_NotReturnedByGetActive()
    {
        var tenantId = Guid.NewGuid();
        WebhookSubscription sub = BuildSubscription("test.event", tenantId, WebhookSubscriptionStatus.Active);
        _store.Add(sub);

        await _store.SuspendAsync(sub.Id, "system", "suspended", TestContext.Current.CancellationToken);

        IReadOnlyList<WebhookSubscription> result =
            await _store.GetActiveSubscriptionsAsync("test.event", tenantId, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // ActivateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ActivateAsync_SetsStatusToActive()
    {
        WebhookSubscription sub = BuildSubscription("test.event", null, WebhookSubscriptionStatus.Suspended);
        _store.Add(sub);

        await _store.ActivateAsync(sub.Id, TestContext.Current.CancellationToken);

        WebhookSubscription? updated = await _store.FindByIdAsync(sub.Id, TestContext.Current.CancellationToken);
        updated!.Status.ShouldBe(WebhookSubscriptionStatus.Active);
        updated.SuspendedAt.ShouldBeNull();
        updated.SuspendedBy.ShouldBeNull();
        updated.ConsecutiveFailureCount.ShouldBe(0);
    }

    [Fact]
    public async Task ActivateAsync_NotSuspended_ThrowsConflictException()
    {
        WebhookSubscription sub = BuildSubscription("test.event", null, WebhookSubscriptionStatus.Active);
        _store.Add(sub);

        Func<Task> act = () => _store.ActivateAsync(sub.Id, TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ConflictException>(act);
    }

    // -------------------------------------------------------------------------
    // DeleteAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_RemovesSubscription()
    {
        WebhookSubscription sub = BuildSubscription("test.event", null, WebhookSubscriptionStatus.Active);
        _store.Add(sub);

        await _store.DeleteAsync(sub.Id, TestContext.Current.CancellationToken);

        WebhookSubscription? found = await _store.FindByIdAsync(sub.Id, TestContext.Current.CancellationToken);
        found.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ThrowsEntityNotFoundException()
    {
        Func<Task> act = () => _store.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        await Should.ThrowAsync<EntityNotFoundException>(act);
    }

    // -------------------------------------------------------------------------
    // RotateSigningKeyAsync — dual-key delivery model (FU-1a)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RotateSigningKeyAsync_TwoRotations_RetiresPreviousActives()
    {
        WebhookSubscription sub = BuildSubscription("test.event", null, WebhookSubscriptionStatus.Active);
        _store.Add(sub);

        WebhookSigningKeyRotatedResult first = await ((IWebhookSigningKeyWriter)_store)
            .RotateSigningKeyAsync(sub.Id, retiredKeyGracePeriod: null, TestContext.Current.CancellationToken);

        await ((IWebhookSigningKeyWriter)_store)
            .RotateSigningKeyAsync(sub.Id, retiredKeyGracePeriod: null, TestContext.Current.CancellationToken);

        // Initial seeded key + 2 rotation keys = 3 total. Exactly one is Active;
        // the other two are Retired with a grace-period ExpiresAt.
        WebhookSubscription? updated = await _store.FindByIdAsync(sub.Id, TestContext.Current.CancellationToken);
        updated!.SigningKeys.Count.ShouldBe(3);
        updated.SigningKeys.Count(k => k.Status == WebhookSigningKeyStatus.Active).ShouldBe(1);
        WebhookSigningKey retired = updated.SigningKeys.Single(k => k.Id == first.KeyId);
        retired.Status.ShouldBe(WebhookSigningKeyStatus.Retired);
        retired.ExpiresAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task RevokeSigningKeyAsync_LastActive_Throws()
    {
        WebhookSubscription sub = BuildSubscription("test.event", null, WebhookSubscriptionStatus.Active);
        _store.Add(sub);

        WebhookSigningKeyRotatedResult result = await ((IWebhookSigningKeyWriter)_store)
            .RotateSigningKeyAsync(sub.Id, retiredKeyGracePeriod: null, TestContext.Current.CancellationToken);

        Func<Task> act = () => ((IWebhookSigningKeyWriter)_store)
            .RevokeSigningKeyAsync(sub.Id, result.KeyId, TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(act);
    }

    [Fact]
    public async Task GetForSubscriptionAsync_ReturnsAllKeys()
    {
        WebhookSubscription sub = BuildSubscription("test.event", null, WebhookSubscriptionStatus.Active);
        _store.Add(sub);

        await ((IWebhookSigningKeyWriter)_store)
            .RotateSigningKeyAsync(sub.Id, retiredKeyGracePeriod: null, TestContext.Current.CancellationToken);
        await ((IWebhookSigningKeyWriter)_store)
            .RotateSigningKeyAsync(sub.Id, retiredKeyGracePeriod: null, TestContext.Current.CancellationToken);

        IReadOnlyList<WebhookSigningKey> keys = await ((IWebhookSigningKeyReader)_store)
            .GetForSubscriptionAsync(sub.Id, TestContext.Current.CancellationToken);

        // Initial seeded key + 2 rotation keys.
        keys.Count.ShouldBe(3);
    }

    // -------------------------------------------------------------------------
    // UpdateTargetUrlAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateTargetUrlAsync_UpdatesUrl()
    {
        WebhookSubscription sub = BuildSubscription("test.event", null, WebhookSubscriptionStatus.Active);
        _store.Add(sub);

        await _store.UpdateTargetUrlAsync(sub.Id, "https://new-url.com/hook", TestContext.Current.CancellationToken);

        WebhookSubscription? updated = await _store.FindByIdAsync(sub.Id, TestContext.Current.CancellationToken);
        updated!.TargetUrl.Value.ShouldBe("https://new-url.com/hook");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static WebhookSubscription BuildSubscription(
        string eventType,
        Guid? tenantId,
        WebhookSubscriptionStatus status)
    {
        var sub = WebhookSubscription.Create(
            Guid.NewGuid(), "https://example.com/webhook", eventType,
            signingKeyId: Guid.NewGuid(), protectedSecret: "protected-secret", createdAt: DateTimeOffset.UtcNow, tenantId: tenantId);

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
