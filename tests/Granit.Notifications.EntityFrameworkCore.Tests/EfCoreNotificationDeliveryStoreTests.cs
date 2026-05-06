// =============================================================================
// Tests - EfCoreNotificationDeliveryStore
// =============================================================================
// Verifies claim/finalization audit trail semantics (GH #947) plus retention deletes.
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
    public async Task TryAcquire_then_Complete_persists_success()
    {
        NotificationDeliveryAttempt claim = BuildClaim(isSuccess: null);

        (await _store.TryAcquireDeliveryAttemptAsync(claim, TestContext.Current.CancellationToken)).ShouldBeTrue();
        await _store.CompleteDeliveryAttemptAsync(
            claim.DeliveryId,
            success: true,
            durationMilliseconds: 150,
            errorMessage: null,
            TestContext.Current.CancellationToken);

        await using NotificationsDbContext db = _factory.CreateDbContext();
        NotificationDeliveryAttempt? result = await db.DeliveryAttempts
            .AsNoTracking()
            .SingleAsync(a => a.DeliveryId == claim.DeliveryId, TestContext.Current.CancellationToken);

        result.NotificationId.ShouldBe(claim.NotificationId);
        result.ChannelName.ShouldBe(claim.ChannelName);
        result.IsSuccess.ShouldBe(true);
        result.DurationMs.ShouldBe(150);
        result.RecipientUserId.ShouldBe(claim.RecipientUserId);
    }

    [Fact]
    public async Task Multiple_delivery_ids_for_same_notification_all_persist()
    {
        var notificationId = Guid.NewGuid();
        NotificationDeliveryAttempt attempt1 =
            BuildClaim(notificationId: notificationId, channelName: "email", isSuccess: null);
        NotificationDeliveryAttempt attempt2 = BuildClaim(notificationId: notificationId, channelName: "email");
        NotificationDeliveryAttempt attempt3 =
            BuildClaim(notificationId: notificationId, channelName: "sms");

        (await _store.TryAcquireDeliveryAttemptAsync(attempt1, TestContext.Current.CancellationToken)).ShouldBeTrue();
        await _store.CompleteDeliveryAttemptAsync(
            attempt1.DeliveryId,
            false,
            durationMilliseconds: 1,
            errorMessage: "SMTP timeout",
            TestContext.Current.CancellationToken);

        (await _store.TryAcquireDeliveryAttemptAsync(attempt2, TestContext.Current.CancellationToken)).ShouldBeTrue();
        await _store.CompleteDeliveryAttemptAsync(
            attempt2.DeliveryId,
            true,
            durationMilliseconds: 2,
            null,
            TestContext.Current.CancellationToken);

        (await _store.TryAcquireDeliveryAttemptAsync(attempt3, TestContext.Current.CancellationToken)).ShouldBeTrue();
        await _store.CompleteDeliveryAttemptAsync(
            attempt3.DeliveryId,
            true,
            durationMilliseconds: 3,
            null,
            TestContext.Current.CancellationToken);

        await using NotificationsDbContext db = _factory.CreateDbContext();
        List<NotificationDeliveryAttempt> all = await db.DeliveryAttempts
            .Where(a => a.NotificationId == notificationId)
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);

        all.Count.ShouldBe(3);
        all.Where(a => a.ChannelName == "email").Count().ShouldBe(2);
        all.Where(a => a.IsSuccess == true).Count().ShouldBe(2);
        all.Single(a => a.IsSuccess == false).ErrorMessage.ShouldBe("SMTP timeout");
    }

    [Fact]
    public async Task After_failed_delivery_second_acquire_resumes_terminal_false_row()
    {
        NotificationDeliveryAttempt first = BuildClaim(isSuccess: null);

        (await _store.TryAcquireDeliveryAttemptAsync(first, TestContext.Current.CancellationToken)).ShouldBeTrue();
        await _store.CompleteDeliveryAttemptAsync(
            first.DeliveryId,
            false,
            durationMilliseconds: 5,
            "channel down",
            TestContext.Current.CancellationToken);

        NotificationDeliveryAttempt second = BuildClaim(
            deliveryId: first.DeliveryId,
            notificationId: first.NotificationId,
            channelName: first.ChannelName,
            isSuccess: null);

        (await _store.TryAcquireDeliveryAttemptAsync(second, TestContext.Current.CancellationToken)).ShouldBeTrue();

        await _store.CompleteDeliveryAttemptAsync(
            first.DeliveryId,
            true,
            durationMilliseconds: 9,
            null,
            TestContext.Current.CancellationToken);

        (await _store.HasBeenDeliveredAsync(first.DeliveryId, TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task After_successful_delivery_second_acquire_returns_false()
    {
        NotificationDeliveryAttempt claim = BuildClaim(isSuccess: null);

        (await _store.TryAcquireDeliveryAttemptAsync(claim, TestContext.Current.CancellationToken)).ShouldBeTrue();
        await _store.CompleteDeliveryAttemptAsync(
            claim.DeliveryId,
            success: true,
            durationMilliseconds: 1,
            null,
            TestContext.Current.CancellationToken);

        NotificationDeliveryAttempt phantom = BuildClaim(deliveryId: claim.DeliveryId, isSuccess: null);
        (await _store.TryAcquireDeliveryAttemptAsync(phantom, TestContext.Current.CancellationToken)).ShouldBeFalse();
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

        foreach (Guid deliveryId in new Guid[] { Guid.NewGuid(), Guid.NewGuid() })
        {
            NotificationDeliveryAttempt row = BuildClaim(deliveryId: deliveryId, occurredAt: old, isSuccess: null);
            (await _store.TryAcquireDeliveryAttemptAsync(row, TestContext.Current.CancellationToken)).ShouldBeTrue();
            await _store.CompleteDeliveryAttemptAsync(
                deliveryId,
                true,
                1,
                null,
                TestContext.Current.CancellationToken);
        }

        NotificationDeliveryAttempt recentRow = BuildClaim(occurredAt: recent, isSuccess: null);
        (await _store.TryAcquireDeliveryAttemptAsync(recentRow, TestContext.Current.CancellationToken)).ShouldBeTrue();
        await _store.CompleteDeliveryAttemptAsync(
            recentRow.DeliveryId,
            true,
            1,
            null,
            TestContext.Current.CancellationToken);

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
            NotificationDeliveryAttempt row = BuildClaim(occurredAt: old, isSuccess: null);
            (await _store.TryAcquireDeliveryAttemptAsync(row, TestContext.Current.CancellationToken)).ShouldBeTrue();
            await _store.CompleteDeliveryAttemptAsync(row.DeliveryId, true, 1, null, TestContext.Current.CancellationToken);
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

    private static NotificationDeliveryAttempt BuildClaim(
        Guid? notificationId = null,
        string channelName = "email",
        Guid? deliveryId = null,
        DateTimeOffset? occurredAt = null,
        bool? isSuccess = null) => new()
    {
        Id = Guid.NewGuid(),
        DeliveryId = deliveryId ?? Guid.NewGuid(),
        NotificationId = notificationId ?? Guid.NewGuid(),
        NotificationTypeName = "test.notification",
        ChannelName = channelName,
        RecipientUserId = "user-1",
        TenantId = Guid.NewGuid(),
        OccurredAt = occurredAt ?? DateTimeOffset.UtcNow,
        DurationMs = 0,
        IsSuccess = isSuccess,
        ErrorMessage = null,
    };
}
