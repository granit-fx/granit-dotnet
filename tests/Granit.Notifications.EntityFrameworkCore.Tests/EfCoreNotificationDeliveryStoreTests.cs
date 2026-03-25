// =============================================================================
// Tests - EfCoreNotificationDeliveryStore
// =============================================================================
// Verifies INSERT-only ISO 27001 audit trail: record single attempt,
// record multiple attempts for the same notification.
// =============================================================================

using Granit.Notifications.Domain;
using Granit.Notifications.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Notifications.EntityFrameworkCore.Tests;

public sealed class EfCoreNotificationDeliveryStoreTests : IDisposable
{
    private readonly TestDbContextFactory _factory = TestDbContextFactory.Create();
    private readonly EfCoreNotificationDeliveryStore _store;

    public EfCoreNotificationDeliveryStoreTests()
    {
        _store = new EfCoreNotificationDeliveryStore(_factory);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task RecordAsync_InsertsAttempt()
    {
        NotificationDeliveryAttempt attempt = BuildAttempt();

        await _store.RecordAsync(attempt, TestContext.Current.CancellationToken);

        await using NotificationsDbContext db = _factory.CreateDbContext();
        NotificationDeliveryAttempt? result = await db.DeliveryAttempts.FindAsync([attempt.Id], TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.NotificationId.ShouldBe(attempt.NotificationId);
        result.ChannelName.ShouldBe(attempt.ChannelName);
        result.IsSuccess.ShouldBe(attempt.IsSuccess);
        result.RecipientUserId.ShouldBe(attempt.RecipientUserId);
    }

    [Fact]
    public async Task RecordAsync_MultipleAttempts_AllPersisted()
    {
        var notificationId = Guid.NewGuid();
        NotificationDeliveryAttempt attempt1 = BuildAttempt(notificationId: notificationId, channelName: "email", isSuccess: false, errorMessage: "SMTP timeout");
        NotificationDeliveryAttempt attempt2 = BuildAttempt(notificationId: notificationId, channelName: "email", isSuccess: true);
        NotificationDeliveryAttempt attempt3 = BuildAttempt(notificationId: notificationId, channelName: "sms", isSuccess: true);

        await _store.RecordAsync(attempt1, TestContext.Current.CancellationToken);
        await _store.RecordAsync(attempt2, TestContext.Current.CancellationToken);
        await _store.RecordAsync(attempt3, TestContext.Current.CancellationToken);

        await using NotificationsDbContext db = _factory.CreateDbContext();
        List<NotificationDeliveryAttempt> all = await db.DeliveryAttempts
            .Where(a => a.NotificationId == notificationId)
            .ToListAsync(TestContext.Current.CancellationToken);

        all.Count.ShouldBe(3);
        all.Where(a => a.ChannelName == "email").Count().ShouldBe(2);
        all.Where(a => a.IsSuccess).Count().ShouldBe(2);
        all.Single(a => !a.IsSuccess).ErrorMessage.ShouldBe("SMTP timeout");
    }

    // -------------------------------------------------------------------------
    // DeleteBeforeAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteBeforeAsync_DeletesAttemptsBeforeCutoff()
    {
        DateTimeOffset old = new(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset recent = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset cutoff = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

        await _store.RecordAsync(BuildAttempt(occurredAt: old), TestContext.Current.CancellationToken);
        await _store.RecordAsync(BuildAttempt(occurredAt: old), TestContext.Current.CancellationToken);
        await _store.RecordAsync(BuildAttempt(occurredAt: recent), TestContext.Current.CancellationToken);

        int deleted = await _store.DeleteBeforeAsync(cutoff, 1000, TestContext.Current.CancellationToken);

        deleted.ShouldBe(2);

        await using NotificationsDbContext db = _factory.CreateDbContext();
        List<NotificationDeliveryAttempt> remaining = await db.DeliveryAttempts
            .ToListAsync(TestContext.Current.CancellationToken);
        remaining.Count.ShouldBe(1);
        remaining[0].OccurredAt.ShouldBe(recent);
    }

    [Fact]
    public async Task DeleteBeforeAsync_RespectsPageSize()
    {
        DateTimeOffset old = new(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset cutoff = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

        for (int i = 0; i < 5; i++)
        {
            await _store.RecordAsync(BuildAttempt(occurredAt: old), TestContext.Current.CancellationToken);
        }

        int deleted = await _store.DeleteBeforeAsync(cutoff, 3, TestContext.Current.CancellationToken);

        deleted.ShouldBe(3);

        await using NotificationsDbContext db = _factory.CreateDbContext();
        int remaining = await db.DeliveryAttempts.CountAsync(TestContext.Current.CancellationToken);
        remaining.ShouldBe(2);
    }

    [Fact]
    public async Task DeleteBeforeAsync_ReturnsZeroWhenNothingToDelete()
    {
        int deleted = await _store.DeleteBeforeAsync(
            DateTimeOffset.UtcNow, 1000, TestContext.Current.CancellationToken);

        deleted.ShouldBe(0);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static NotificationDeliveryAttempt BuildAttempt(
        Guid? notificationId = null,
        string channelName = "email",
        bool isSuccess = true,
        string? errorMessage = null,
        DateTimeOffset? occurredAt = null) => new()
        {
            Id = Guid.NewGuid(),
            DeliveryId = Guid.NewGuid(),
            NotificationId = notificationId ?? Guid.NewGuid(),
            NotificationTypeName = "test.notification",
            ChannelName = channelName,
            RecipientUserId = "user-1",
            TenantId = Guid.NewGuid(),
            OccurredAt = occurredAt ?? DateTimeOffset.UtcNow,
            DurationMs = 150,
            IsSuccess = isSuccess,
            ErrorMessage = errorMessage,
        };
}
