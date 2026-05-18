using Granit.Persistence.EntityFrameworkCore;
using Granit.Webhooks.Domain;
using Granit.Webhooks.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.EntityFrameworkCore.Tests;

public sealed class WebhooksDbContextTests : IAsyncDisposable
{
    private readonly DbContextOptions<WebhooksDbContext> _options;
    private readonly TestDataFilter _dataFilter = new();

    public WebhooksDbContextTests()
    {
        _options = new DbContextOptionsBuilder<WebhooksDbContext>()
            .UseInMemoryDatabase(databaseName: $"webhooks-ctx-{Guid.NewGuid()}")
            .Options;
    }

    public async ValueTask DisposeAsync()
    {
        await using (WebhooksDbContext context = new(_options, GranitDesignTime.CurrentTenant, _dataFilter.Filter))
        {
            await context.Database.EnsureDeletedAsync();
        }
        _dataFilter.Dispose();
    }

    [Fact]
    public async Task Can_add_and_retrieve_subscription()
    {
        // Arrange
        var id = Guid.NewGuid();
        await using (WebhooksDbContext context = new(_options, GranitDesignTime.CurrentTenant, _dataFilter.Filter))
        {
            context.WebhookSubscriptions.Add(
                WebhookSubscription.Create(
                    id, "https://example.com/hook", "test.event",
                    signingKeyId: Guid.NewGuid(), protectedSecret: "secret", createdAt: DateTimeOffset.UtcNow));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        await using (WebhooksDbContext context = new(_options, GranitDesignTime.CurrentTenant, _dataFilter.Filter))
        {
            WebhookSubscription? subscription = await context.WebhookSubscriptions.FindAsync([id], TestContext.Current.CancellationToken);

            // Assert
            subscription.ShouldNotBeNull();
            subscription!.TargetUrl.Value.ShouldBe("https://example.com/hook");
        }
    }

    [Fact]
    public async Task Can_add_and_retrieve_delivery_attempt()
    {
        // Arrange
        var id = Guid.NewGuid();
        await using (WebhooksDbContext context = new(_options, GranitDesignTime.CurrentTenant, _dataFilter.Filter))
        {
            context.WebhookDeliveryAttempts.Add(new WebhookDeliveryAttempt
            {
                Id = id,
                DeliveryId = Guid.NewGuid(),
                SubscriptionId = Guid.NewGuid(),
                EventType = "test.event",
                TargetUrl = "https://example.com/hook",
                PayloadHash = "abc",
                OccurredAt = DateTimeOffset.UtcNow,
                DurationMs = 100,
                IsSuccess = true,
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        await using (WebhooksDbContext context = new(_options, GranitDesignTime.CurrentTenant, _dataFilter.Filter))
        {
            WebhookDeliveryAttempt? attempt = await context.WebhookDeliveryAttempts.FindAsync([id], TestContext.Current.CancellationToken);

            // Assert
            attempt.ShouldNotBeNull();
            attempt!.IsSuccess.ShouldBeTrue();
        }
    }

    [Fact]
    public void DbSets_are_accessible()
    {
        using WebhooksDbContext context = new(_options, GranitDesignTime.CurrentTenant, _dataFilter.Filter);
        context.WebhookSubscriptions.ShouldNotBeNull();
        context.WebhookDeliveryAttempts.ShouldNotBeNull();
    }
}
