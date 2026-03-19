using Granit.Webhooks.Domain;
using Granit.Webhooks.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.EntityFrameworkCore.Tests;

public sealed class EfWebhookSubscriptionStoreTests : IAsyncDisposable
{
    private readonly DbContextOptions<WebhooksDbContext> _options;
    private readonly IDbContextFactory<WebhooksDbContext> _contextFactory;
    private readonly EfWebhookSubscriptionStore _sut;

    public EfWebhookSubscriptionStoreTests()
    {
        _options = new DbContextOptionsBuilder<WebhooksDbContext>()
            .UseInMemoryDatabase(databaseName: $"webhooks-sub-{Guid.NewGuid()}")
            .Options;

        _contextFactory = new TestWebhooksDbContextFactory(_options);
        _sut = new EfWebhookSubscriptionStore(_contextFactory);
    }

    public async ValueTask DisposeAsync()
    {
        await using WebhooksDbContext context = new(_options);
        await context.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task GetActiveSubscriptionsAsync_filters_by_event_type_and_active_status()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await SeedSubscriptions(
            CreateSubscription("doc.uploaded", tenantId, WebhookSubscriptionStatus.Active),
            CreateSubscription("doc.uploaded", tenantId, WebhookSubscriptionStatus.Deactivated),
            CreateSubscription("user.created", tenantId, WebhookSubscriptionStatus.Active));

        // Act
        IReadOnlyList<WebhookSubscription> result =
            await _sut.GetActiveSubscriptionsAsync("doc.uploaded", tenantId, TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(1);
        result[0].EventType.ShouldBe("doc.uploaded");
    }

    [Fact]
    public async Task GetActiveSubscriptionsAsync_includes_global_subscriptions()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await SeedSubscriptions(
            CreateSubscription("doc.uploaded", tenantId, WebhookSubscriptionStatus.Active),
            CreateSubscription("doc.uploaded", null, WebhookSubscriptionStatus.Active));

        // Act
        IReadOnlyList<WebhookSubscription> result =
            await _sut.GetActiveSubscriptionsAsync("doc.uploaded", tenantId, TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetActiveSubscriptionsAsync_excludes_other_tenants()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await SeedSubscriptions(
            CreateSubscription("doc.uploaded", tenantA, WebhookSubscriptionStatus.Active),
            CreateSubscription("doc.uploaded", tenantB, WebhookSubscriptionStatus.Active));

        // Act
        IReadOnlyList<WebhookSubscription> result =
            await _sut.GetActiveSubscriptionsAsync("doc.uploaded", tenantA, TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(1);
        result[0].TenantId.ShouldBe(tenantA);
    }

    [Fact]
    public async Task GetActiveSubscriptionsAsync_returns_empty_when_no_match()
    {
        // Act
        IReadOnlyList<WebhookSubscription> result =
            await _sut.GetActiveSubscriptionsAsync("doc.uploaded", Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task FindByIdAsync_returns_subscription_when_exists()
    {
        // Arrange
        WebhookSubscription subscription = CreateSubscription("doc.uploaded", Guid.NewGuid());
        await SeedSubscriptions(subscription);

        // Act
        WebhookSubscription? result =
            await _sut.FindByIdAsync(subscription.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result!.Id.ShouldBe(subscription.Id);
    }

    [Fact]
    public async Task FindByIdAsync_returns_null_when_not_found()
    {
        // Act
        WebhookSubscription? result =
            await _sut.FindByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task DeactivateAsync_sets_status_and_reason()
    {
        // Arrange
        WebhookSubscription subscription = CreateSubscription("doc.uploaded", Guid.NewGuid());
        await SeedSubscriptions(subscription);

        // Act
        await _sut.DeactivateAsync(subscription.Id, "User requested", TestContext.Current.CancellationToken);

        // Assert
        await using WebhooksDbContext context = new(_options);
        WebhookSubscription? updated = await context.WebhookSubscriptions.FindAsync([subscription.Id], TestContext.Current.CancellationToken);
        updated!.Status.ShouldBe(WebhookSubscriptionStatus.Deactivated);
        updated.DeactivationReason.ShouldBe("User requested");
    }

    [Fact]
    public async Task DeactivateAsync_no_op_when_not_found()
    {
        // Act & Assert — should not throw
        await Should.NotThrowAsync(async () =>
            await _sut.DeactivateAsync(Guid.NewGuid(), "reason", TestContext.Current.CancellationToken));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task SeedSubscriptions(params WebhookSubscription[] subscriptions)
    {
        await using WebhooksDbContext context = new(_options);
        context.WebhookSubscriptions.AddRange(subscriptions);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static WebhookSubscription CreateSubscription(
        string eventType,
        Guid? tenantId,
        WebhookSubscriptionStatus status = WebhookSubscriptionStatus.Active)
    {
        var sub = WebhookSubscription.Create(
            Guid.NewGuid(),
            $"https://example.com/{Guid.NewGuid()}",
            eventType,
            "protected-secret",
            tenantId);

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
