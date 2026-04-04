// =============================================================================
// Tests - EntityTrackingInterceptor
// =============================================================================
// Verifies the SaveChanges interceptor that detects modifications on
// ITrackedEntity implementations and publishes notifications to entity
// followers (Odoo-style auto-tracking).
// =============================================================================

using Granit.Domain;
using Granit.Notifications.Abstractions;
using Granit.Notifications.EntityFrameworkCore.Internal;
using Granit.Timing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.EntityFrameworkCore.Tests;

public sealed class EntityTrackingInterceptorTests : IDisposable
{
    private static readonly DateTimeOffset FixedNow = new(2026, 2, 28, 10, 0, 0, TimeSpan.Zero);

    private readonly INotificationPublisher _publisher = Substitute.For<INotificationPublisher>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<TrackingTestDbContext> _options;
    private readonly EntityTrackingInterceptor _interceptor;

    public EntityTrackingInterceptorTests()
    {
        _clock.Now.Returns(FixedNow);

        _interceptor = new EntityTrackingInterceptor(_publisher, _clock);

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        DbContextOptionsBuilder<TrackingTestDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(_connection);
        optionsBuilder.AddInterceptors(_interceptor);

        _options = optionsBuilder.Options;

        using (TrackingTestDbContext db = new(_options))
        {
            db.Database.EnsureCreated();
        }
    }

    public void Dispose() => _connection.Dispose();

    // -------------------------------------------------------------------------
    // SavingChangesAsync — null context
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SavingChangesAsync_NullContext_ReturnsBaseResult()
    {
        Microsoft.EntityFrameworkCore.Diagnostics.ILoggingOptions loggingOptions =
            Substitute.For<Microsoft.EntityFrameworkCore.Diagnostics.ILoggingOptions>();

        EventDefinitionBase eventDefinition = Substitute.For<EventDefinitionBase>(
            loggingOptions,
            new Microsoft.Extensions.Logging.EventId(1, "Test"),
            Microsoft.Extensions.Logging.LogLevel.Information,
            "Test");

        DbContextEventData eventData = new(
            eventDefinition,
            static (_, _) => "Test",
            context: null);

        InterceptionResult<int> result = new();

        InterceptionResult<int> actual = await _interceptor.SavingChangesAsync(
            eventData,
            result,
            TestContext.Current.CancellationToken);

        await _publisher.DidNotReceive().PublishToEntityFollowersAsync(
            Arg.Any<NotificationType<EntityStateChangedData>>(),
            Arg.Any<EntityStateChangedData>(),
            Arg.Any<EntityReference>(),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // SavingChangesAsync — modified tracked entity publishes notification
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SavingChangesAsync_ModifiedTrackedEntity_PublishesNotification()
    {
        await using TrackingTestDbContext db = new(_options);

        TrackedOrder order = new() { Id = Guid.NewGuid(), Status = "Pending", Description = "Initial" };
        db.Orders.Add(order);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        order.Status = "Shipped";
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _publisher.Received(1).PublishToEntityFollowersAsync(
            Arg.Is<NotificationType<EntityStateChangedData>>(t => t.Name == "order.status_changed"),
            Arg.Is<EntityStateChangedData>(d =>
                d.EntityType == "Order" &&
                d.EntityId == order.Id.ToString() &&
                d.PropertyName == "Status" &&
                d.OldValue == "Pending" &&
                d.NewValue == "Shipped" &&
                d.ChangedAt == FixedNow),
            Arg.Is<EntityReference>(r => r.EntityType == "Order" && r.EntityId == order.Id.ToString()),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // SavingChangesAsync — unmodified entity does not publish
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SavingChangesAsync_UnmodifiedEntity_DoesNotPublish()
    {
        await using TrackingTestDbContext db = new(_options);

        TrackedOrder order = new() { Id = Guid.NewGuid(), Status = "Pending", Description = "Initial" };
        db.Orders.Add(order);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Reset the publisher call count after the initial Add (which is EntityState.Added, not Modified)
        _publisher.ClearReceivedCalls();

        // Load and save without modifying — no notification expected
        TrackedOrder? loaded = await db.Orders.FindAsync([order.Id], TestContext.Current.CancellationToken);
        loaded.ShouldNotBeNull();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _publisher.DidNotReceive().PublishToEntityFollowersAsync(
            Arg.Any<NotificationType<EntityStateChangedData>>(),
            Arg.Any<EntityStateChangedData>(),
            Arg.Any<EntityReference>(),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // SavingChangesAsync — non-ITrackedEntity does not publish
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SavingChangesAsync_NonTrackedEntity_DoesNotPublish()
    {
        await using TrackingTestDbContext db = new(_options);

        UntrackedProduct product = new() { Id = Guid.NewGuid(), Name = "Widget" };
        db.Products.Add(product);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        _publisher.ClearReceivedCalls();

        product.Name = "Super Widget";
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _publisher.DidNotReceive().PublishToEntityFollowersAsync(
            Arg.Any<NotificationType<EntityStateChangedData>>(),
            Arg.Any<EntityStateChangedData>(),
            Arg.Any<EntityReference>(),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // SavingChangesAsync — multiple property changes publish multiple notifications
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SavingChangesAsync_MultiplePropertyChanges_PublishesMultipleNotifications()
    {
        await using TrackingTestDbContext db = new(_options);

        TrackedOrder order = new() { Id = Guid.NewGuid(), Status = "Pending", Description = "First version" };
        db.Orders.Add(order);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        _publisher.ClearReceivedCalls();

        order.Status = "Confirmed";
        order.Description = "Updated version";
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _publisher.Received(2).PublishToEntityFollowersAsync(
            Arg.Any<NotificationType<EntityStateChangedData>>(),
            Arg.Any<EntityStateChangedData>(),
            Arg.Any<EntityReference>(),
            Arg.Any<CancellationToken>());

        await _publisher.Received(1).PublishToEntityFollowersAsync(
            Arg.Is<NotificationType<EntityStateChangedData>>(t => t.Name == "order.status_changed"),
            Arg.Is<EntityStateChangedData>(d =>
                d.PropertyName == "Status" &&
                d.OldValue == "Pending" &&
                d.NewValue == "Confirmed"),
            Arg.Any<EntityReference>(),
            Arg.Any<CancellationToken>());

        await _publisher.Received(1).PublishToEntityFollowersAsync(
            Arg.Is<NotificationType<EntityStateChangedData>>(t => t.Name == "order.description_changed"),
            Arg.Is<EntityStateChangedData>(d =>
                d.PropertyName == "Description" &&
                d.OldValue == "First version" &&
                d.NewValue == "Updated version"),
            Arg.Any<EntityReference>(),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // SavingChangesAsync — unchanged tracked property is not published
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SavingChangesAsync_UnchangedTrackedProperty_DoesNotPublish()
    {
        await using TrackingTestDbContext db = new(_options);

        TrackedOrder order = new() { Id = Guid.NewGuid(), Status = "Pending", Description = "Some description" };
        db.Orders.Add(order);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        _publisher.ClearReceivedCalls();

        // Only modify Description, not Status
        order.Description = "New description";
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Should only publish for Description, not for Status
        await _publisher.Received(1).PublishToEntityFollowersAsync(
            Arg.Any<NotificationType<EntityStateChangedData>>(),
            Arg.Any<EntityStateChangedData>(),
            Arg.Any<EntityReference>(),
            Arg.Any<CancellationToken>());

        await _publisher.Received(1).PublishToEntityFollowersAsync(
            Arg.Is<NotificationType<EntityStateChangedData>>(t => t.Name == "order.description_changed"),
            Arg.Is<EntityStateChangedData>(d => d.PropertyName == "Description"),
            Arg.Any<EntityReference>(),
            Arg.Any<CancellationToken>());

        await _publisher.DidNotReceive().PublishToEntityFollowersAsync(
            Arg.Any<NotificationType<EntityStateChangedData>>(),
            Arg.Is<EntityStateChangedData>(d => d.PropertyName == "Status"),
            Arg.Any<EntityReference>(),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // SavingChangesAsync — notification severity from config
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SavingChangesAsync_UsesConfiguredSeverity()
    {
        await using TrackingTestDbContext db = new(_options);

        TrackedOrder order = new() { Id = Guid.NewGuid(), Status = "Draft", Description = "Initial" };
        db.Orders.Add(order);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        _publisher.ClearReceivedCalls();

        order.Status = "Cancelled";
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _publisher.Received(1).PublishToEntityFollowersAsync(
            Arg.Is<NotificationType<EntityStateChangedData>>(t =>
                t.Name == "order.status_changed" &&
                t.DefaultSeverity == NotificationSeverity.Warning),
            Arg.Any<EntityStateChangedData>(),
            Arg.Any<EntityReference>(),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // SavingChangesAsync — non-tracked property change does not publish
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SavingChangesAsync_NonTrackedPropertyChange_DoesNotPublish()
    {
        await using TrackingTestDbContext db = new(_options);

        TrackedOrder order = new() { Id = Guid.NewGuid(), Status = "Pending", Description = "Initial", InternalNotes = "Notes" };
        db.Orders.Add(order);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        _publisher.ClearReceivedCalls();

        // InternalNotes is not in TrackedProperties, so no notification
        order.InternalNotes = "Updated notes";
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _publisher.DidNotReceive().PublishToEntityFollowersAsync(
            Arg.Any<NotificationType<EntityStateChangedData>>(),
            Arg.Any<EntityStateChangedData>(),
            Arg.Any<EntityReference>(),
            Arg.Any<CancellationToken>());
    }
}

// =============================================================================
// Test entities
// =============================================================================

/// <summary>
/// Entity implementing <see cref="ITrackedEntity"/> for testing the interceptor.
/// Has two tracked properties (Status and Description) and one untracked (InternalNotes).
/// </summary>
internal sealed class TrackedOrder : ITrackedEntity
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? InternalNotes { get; set; }

    public static string EntityTypeName => "Order";

    public static IReadOnlyDictionary<string, TrackedPropertyConfig> TrackedProperties { get; } =
        new Dictionary<string, TrackedPropertyConfig>
        {
            ["Status"] = new TrackedPropertyConfig
            {
                NotificationTypeName = "order.status_changed",
                Severity = NotificationSeverity.Warning,
            },
            ["Description"] = new TrackedPropertyConfig
            {
                NotificationTypeName = "order.description_changed",
                Severity = NotificationSeverity.Info,
            },
        };

    public string GetEntityId() => Id.ToString();
}

/// <summary>
/// Entity that does NOT implement <see cref="ITrackedEntity"/>.
/// Used to verify that the interceptor ignores non-tracked entities.
/// </summary>
internal sealed class UntrackedProduct
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

// =============================================================================
// Test DbContext
// =============================================================================

/// <summary>
/// Minimal DbContext that registers both <see cref="TrackedOrder"/> and
/// <see cref="UntrackedProduct"/> for interceptor integration tests.
/// </summary>
internal sealed class TrackingTestDbContext : DbContext
{
    public DbSet<TrackedOrder> Orders => Set<TrackedOrder>();
    public DbSet<UntrackedProduct> Products => Set<UntrackedProduct>();

    public TrackingTestDbContext(DbContextOptions<TrackingTestDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TrackedOrder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.Description).IsRequired();
        });

        modelBuilder.Entity<UntrackedProduct>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
        });
    }
}
