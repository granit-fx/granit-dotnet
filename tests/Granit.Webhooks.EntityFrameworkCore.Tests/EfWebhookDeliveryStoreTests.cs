using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
using Granit.Webhooks.Domain;
using Granit.Webhooks.EntityFrameworkCore.Internal;
using Granit.Webhooks.Messages;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.EntityFrameworkCore.Tests;

public sealed class EfWebhookDeliveryStoreTests : IAsyncDisposable
{
    private readonly DateTimeOffset _now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly IClock _clock;
    private readonly IDbContextFactory<WebhooksDbContext> _contextFactory;
    private readonly DbContextOptions<WebhooksDbContext> _options;
    private readonly EfWebhookDeliveryStore _sut;

    public EfWebhookDeliveryStoreTests()
    {
        _clock = Substitute.For<IClock>();
        _clock.Now.Returns(_ => _now);

        _options = new DbContextOptionsBuilder<WebhooksDbContext>()
            .UseInMemoryDatabase(databaseName: $"webhooks-delivery-{Guid.NewGuid()}")
            .Options;

        _contextFactory = new TestWebhooksDbContextFactory(_options);
        _sut = new EfWebhookDeliveryStore(_contextFactory, Substitute.For<ICurrentTenant>(), _clock, new SimpleGuidGenerator());
    }

    public async ValueTask DisposeAsync()
    {
        await using WebhooksDbContext context = new(_options);
        await context.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task RecordSuccessAsync_creates_delivery_attempt()
    {
        // Arrange
        SendWebhookCommand command = BuildCommand();

        // Act
        await _sut.RecordSuccessAsync(command, 200, 42, "abc123", null, TestContext.Current.CancellationToken);

        // Assert
        await using WebhooksDbContext context = new(_options);
        WebhookDeliveryAttempt attempt = await context.WebhookDeliveryAttempts.SingleAsync(TestContext.Current.CancellationToken);
        attempt.DeliveryId.ShouldBe(command.DeliveryId);
        attempt.SubscriptionId.ShouldBe(command.SubscriptionId);
        attempt.HttpStatusCode.ShouldBe(200);
        attempt.DurationMs.ShouldBe(42);
        attempt.PayloadHash.ShouldBe("abc123");
        attempt.IsSuccess.ShouldBeTrue();
        attempt.ErrorMessage.ShouldBeNull();
        attempt.OccurredAt.ShouldBe(_now);
    }

    [Fact]
    public async Task RecordSuccessAsync_resets_subscription_failure_count()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        await SeedSubscriptionAsync(subscriptionId, consecutiveFailures: 5);
        SendWebhookCommand command = BuildCommand(subscriptionId: subscriptionId);

        // Act
        await _sut.RecordSuccessAsync(command, 200, 10, "hash", null, TestContext.Current.CancellationToken);

        // Assert
        await using WebhooksDbContext context = new(_options);
        WebhookSubscription? subscription = await context.WebhookSubscriptions.FindAsync([subscriptionId], TestContext.Current.CancellationToken);
        subscription.ShouldNotBeNull();
        subscription!.ConsecutiveFailureCount.ShouldBe(0);
        subscription.LastSuccessAt.ShouldBe(_now);
    }

    [Fact]
    public async Task RecordSuccessAsync_no_op_when_subscription_not_found()
    {
        // Arrange — no subscription seeded
        SendWebhookCommand command = BuildCommand();

        // Act & Assert — should not throw
        await Should.NotThrowAsync(async () =>
            await _sut.RecordSuccessAsync(command, 200, 10, "hash", null, TestContext.Current.CancellationToken));

        await using WebhooksDbContext context = new(_options);
        (await context.WebhookDeliveryAttempts.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task RecordFailureAsync_creates_failure_attempt()
    {
        // Arrange
        SendWebhookCommand command = BuildCommand();

        // Act
        await _sut.RecordFailureAsync(command, 500, 100, "Server Error", null, TestContext.Current.CancellationToken);

        // Assert
        await using WebhooksDbContext context = new(_options);
        WebhookDeliveryAttempt attempt = await context.WebhookDeliveryAttempts.SingleAsync(TestContext.Current.CancellationToken);
        attempt.IsSuccess.ShouldBeFalse();
        attempt.HttpStatusCode.ShouldBe(500);
        attempt.ErrorMessage.ShouldBe("Server Error");
        attempt.PayloadHash.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task RecordFailureAsync_increments_consecutive_failure_count()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        await SeedSubscriptionAsync(subscriptionId, consecutiveFailures: 2);
        SendWebhookCommand command = BuildCommand(subscriptionId: subscriptionId);

        // Act
        await _sut.RecordFailureAsync(command, 500, 50, "Error", null, TestContext.Current.CancellationToken);

        // Assert
        await using WebhooksDbContext context = new(_options);
        WebhookSubscription? subscription = await context.WebhookSubscriptions.FindAsync([subscriptionId], TestContext.Current.CancellationToken);
        subscription!.ConsecutiveFailureCount.ShouldBe(3);
    }

    [Fact]
    public async Task RecordFailureAsync_truncates_long_error_message()
    {
        // Arrange
        SendWebhookCommand command = BuildCommand();
        string longError = new('X', 3000);

        // Act
        await _sut.RecordFailureAsync(command, null, 50, longError, null, TestContext.Current.CancellationToken);

        // Assert
        await using WebhooksDbContext context = new(_options);
        WebhookDeliveryAttempt attempt = await context.WebhookDeliveryAttempts.SingleAsync(TestContext.Current.CancellationToken);
        attempt.ErrorMessage!.Length.ShouldBe(2000);
    }

    [Fact]
    public async Task RecordFailureAsync_with_null_http_status_code()
    {
        // Arrange — timeout scenario
        SendWebhookCommand command = BuildCommand();

        // Act
        await _sut.RecordFailureAsync(command, null, 10000, "Timeout", null, TestContext.Current.CancellationToken);

        // Assert
        await using WebhooksDbContext context = new(_options);
        WebhookDeliveryAttempt attempt = await context.WebhookDeliveryAttempts.SingleAsync(TestContext.Current.CancellationToken);
        attempt.HttpStatusCode.ShouldBeNull();
    }

    [Fact]
    public async Task SuspendSubscriptionAsync_sets_status_and_audit_fields()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        await SeedSubscriptionAsync(subscriptionId);

        // Act
        await _sut.SuspendSubscriptionAsync(subscriptionId, "Too many failures", TestContext.Current.CancellationToken);

        // Assert
        await using WebhooksDbContext context = new(_options);
        WebhookSubscription? subscription = await context.WebhookSubscriptions.FindAsync([subscriptionId], TestContext.Current.CancellationToken);
        subscription!.Status.ShouldBe(WebhookSubscriptionStatus.Suspended);
        subscription.DeactivationReason.ShouldBe("Too many failures");
        subscription.SuspendedAt.ShouldBe(_now);
        subscription.SuspendedBy.ShouldBe("system");
    }

    [Fact]
    public async Task SuspendSubscriptionAsync_no_op_when_not_found()
    {
        // Act & Assert — should not throw
        await Should.NotThrowAsync(async () =>
            await _sut.SuspendSubscriptionAsync(Guid.NewGuid(), "reason", TestContext.Current.CancellationToken));
    }

    // -------------------------------------------------------------------------
    // CountBeforeAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CountBeforeAsync_ReturnsCountOfAttemptsBeforeCutoff()
    {
        DateTimeOffset old = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset recent = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset cutoff = new(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);

        await SeedDeliveryAttemptAsync(old);
        await SeedDeliveryAttemptAsync(old);
        await SeedDeliveryAttemptAsync(recent);

        int count = await _sut.CountBeforeAsync(cutoff, TestContext.Current.CancellationToken);

        count.ShouldBe(2);
    }

    [Fact]
    public async Task CountBeforeAsync_ReturnsZeroWhenEmpty()
    {
        int count = await _sut.CountBeforeAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        count.ShouldBe(0);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task SeedSubscriptionAsync(Guid subscriptionId, int consecutiveFailures = 0)
    {
        var sub = WebhookSubscription.Create(subscriptionId, "https://example.com/hook", "test.event", "protected-secret");

        for (int i = 0; i < consecutiveFailures; i++)
        {
            sub.RecordFailure();
        }

        sub.ClearDomainEvents();

        await using WebhooksDbContext context = new(_options);
        context.WebhookSubscriptions.Add(sub);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedDeliveryAttemptAsync(DateTimeOffset occurredAt)
    {
        await using WebhooksDbContext context = new(_options);
        context.WebhookDeliveryAttempts.Add(new WebhookDeliveryAttempt
        {
            Id = Guid.NewGuid(),
            DeliveryId = Guid.NewGuid(),
            SubscriptionId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            EventType = "test.event",
            TargetUrl = "https://example.com/hook",
            HttpStatusCode = 200,
            PayloadHash = "hash",
            OccurredAt = occurredAt,
            DurationMs = 50,
            IsSuccess = true,
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static SendWebhookCommand BuildCommand(Guid? subscriptionId = null) => new()
    {
        DeliveryId = Guid.NewGuid(),
        SubscriptionId = subscriptionId ?? Guid.NewGuid(),
        TargetUrl = "https://example.com/hook",
        Envelope = new WebhookEnvelope
        {
            EventId = Guid.NewGuid(),
            EventType = "test.event",
            TenantId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            ApiVersion = "1.0",
            Data = System.Text.Json.JsonSerializer.SerializeToElement(new { id = 1 }),
        },
    };
}

internal sealed class TestWebhooksDbContextFactory(DbContextOptions<WebhooksDbContext> options)
    : IDbContextFactory<WebhooksDbContext>
{
    public WebhooksDbContext CreateDbContext() => new(options);

    public Task<WebhooksDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new WebhooksDbContext(options));
}

// =============================================================================
// DeleteBeforeAsync — requires SQLite (ExecuteDeleteAsync not supported by InMemory)
// =============================================================================

public sealed class EfWebhookDeliveryStoreDeleteTests : IDisposable
{
    private readonly TestWebhooksSqliteFactory _factory;
    private readonly EfWebhookDeliveryStore _sut;

    public EfWebhookDeliveryStoreDeleteTests()
    {
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(_ => new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));

        _factory = TestWebhooksSqliteFactory.Create();
        _sut = new EfWebhookDeliveryStore(_factory, Substitute.For<ICurrentTenant>(), clock, new SimpleGuidGenerator());
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task DeleteBeforeAsync_DeletesAttemptsBeforeCutoff()
    {
        // Dates must be older than the 3-year ISO 27001 retention guard.
        DateTimeOffset old = new(2019, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset recent = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset cutoff = new(2022, 6, 1, 0, 0, 0, TimeSpan.Zero);

        await SeedDeliveryAttemptAsync(old);
        await SeedDeliveryAttemptAsync(old);
        await SeedDeliveryAttemptAsync(recent);

        int deleted = await _sut.DeleteBeforeAsync(cutoff, 1000, TestContext.Current.CancellationToken);

        deleted.ShouldBe(2);

        await using WebhooksDbContext context = _factory.CreateDbContext();
        int remaining = await context.WebhookDeliveryAttempts.CountAsync(TestContext.Current.CancellationToken);
        remaining.ShouldBe(1);
    }

    [Fact]
    public async Task DeleteBeforeAsync_RespectsPageSize()
    {
        DateTimeOffset old = new(2019, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset cutoff = new(2022, 6, 1, 0, 0, 0, TimeSpan.Zero);

        for (int i = 0; i < 5; i++)
        {
            await SeedDeliveryAttemptAsync(old);
        }

        int deleted = await _sut.DeleteBeforeAsync(cutoff, 3, TestContext.Current.CancellationToken);

        deleted.ShouldBe(3);

        await using WebhooksDbContext context = _factory.CreateDbContext();
        int remaining = await context.WebhookDeliveryAttempts.CountAsync(TestContext.Current.CancellationToken);
        remaining.ShouldBe(2);
    }

    [Fact]
    public async Task DeleteBeforeAsync_ReturnsZeroWhenNothingToDelete()
    {
        // Cutoff must be older than the 3-year retention guard.
        DateTimeOffset cutoff = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        int deleted = await _sut.DeleteBeforeAsync(cutoff, 1000, TestContext.Current.CancellationToken);

        deleted.ShouldBe(0);
    }

    [Fact]
    public async Task DeleteBeforeAsync_ThrowsWhenCutoffWithinRetentionPeriod()
    {
        // Attempting to delete records within the 3-year retention period should throw.
        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.DeleteBeforeAsync(DateTimeOffset.UtcNow, 1000, TestContext.Current.CancellationToken));
    }

    private async Task SeedDeliveryAttemptAsync(DateTimeOffset occurredAt)
    {
        await using WebhooksDbContext context = _factory.CreateDbContext();
        context.WebhookDeliveryAttempts.Add(new WebhookDeliveryAttempt
        {
            Id = Guid.NewGuid(),
            DeliveryId = Guid.NewGuid(),
            SubscriptionId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            EventType = "test.event",
            TargetUrl = "https://example.com/hook",
            HttpStatusCode = 200,
            PayloadHash = "hash",
            OccurredAt = occurredAt,
            DurationMs = 50,
            IsSuccess = true,
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}

/// <summary>
/// SQLite in-memory factory for tests requiring <c>ExecuteDeleteAsync</c>.
/// </summary>
internal sealed class TestWebhooksSqliteFactory : IDbContextFactory<WebhooksDbContext>, IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly DbContextOptions<WebhooksDbContext> _options;

    private TestWebhooksSqliteFactory(Microsoft.Data.Sqlite.SqliteConnection connection, DbContextOptions<WebhooksDbContext> options)
    {
        _connection = connection;
        _options = options;
    }

    public static TestWebhooksSqliteFactory Create()
    {
        Microsoft.Data.Sqlite.SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptionsBuilder<WebhooksDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(connection);
        optionsBuilder.ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCustomizer, WebhooksSqliteModelCustomizer>();

        DbContextOptions<WebhooksDbContext> options = optionsBuilder.Options;

        using (WebhooksDbContext db = new(options))
        {
            db.Database.EnsureCreated();
        }

        return new TestWebhooksSqliteFactory(connection, options);
    }

    public WebhooksDbContext CreateDbContext() => new(_options);

    public Task<WebhooksDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateDbContext());

    public void Dispose() => _connection.Dispose();
}

internal sealed class WebhooksSqliteModelCustomizer(
    Microsoft.EntityFrameworkCore.Infrastructure.ModelCustomizerDependencies dependencies)
    : Microsoft.EntityFrameworkCore.Infrastructure.RelationalModelCustomizer(dependencies)
{
    private static readonly Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTimeOffset, long> DateTimeOffsetConverter = new(
        v => v.ToUnixTimeMilliseconds(),
        v => DateTimeOffset.FromUnixTimeMilliseconds(v));

    private static readonly Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTimeOffset?, long?> NullableDateTimeOffsetConverter = new(
        v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : null,
        v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : null);

    private static readonly Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<System.Text.Json.JsonElement, string> JsonElementConverter = new(
        v => v.GetRawText(),
        v => System.Text.Json.JsonDocument.Parse(v, default).RootElement);

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableProperty property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(DateTimeOffsetConverter);
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(NullableDateTimeOffsetConverter);
                }
                else if (property.ClrType == typeof(System.Text.Json.JsonElement))
                {
                    property.SetColumnType("TEXT");
                    property.SetValueConverter(JsonElementConverter);
                }
            }
        }
    }
}
