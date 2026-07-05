// =============================================================================
// Tests - EfCoreNotificationDeliveryStore
// =============================================================================
// Verifies claim/finalization audit trail semantics (GH #947) plus retention deletes.
// =============================================================================

using Granit.Notifications.Domain;
using Granit.Notifications.EntityFrameworkCore.Internal;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.EntityFrameworkCore.Tests;

public sealed class EfCoreNotificationDeliveryStoreTests : IDisposable
{
    private readonly TestDbContextFactory _factory = TestDbContextFactory.Create();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly EfCoreNotificationDeliveryStore _store;
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public EfCoreNotificationDeliveryStoreTests()
    {
        _clock.Now.Returns(_ => _now);
        _store = new EfCoreNotificationDeliveryStore(_factory, _clock);
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
        all.Count(a => a.ChannelName == "email").ShouldBe(2);
        all.Count(a => a.IsSuccess == true).ShouldBe(2);
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
            notificationId: first.NotificationId,
            channelName: first.ChannelName,
            deliveryId: first.DeliveryId,
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
    public async Task Resume_after_failure_preserves_prior_failure_audit_context()
    {
        NotificationDeliveryAttempt first = BuildClaim(isSuccess: null);

        (await _store.TryAcquireDeliveryAttemptAsync(first, TestContext.Current.CancellationToken)).ShouldBeTrue();
        await _store.CompleteDeliveryAttemptAsync(
            first.DeliveryId,
            success: false,
            durationMilliseconds: 750,
            errorMessage: "SMTP 421 service unavailable",
            TestContext.Current.CancellationToken);

        NotificationDeliveryAttempt resume = BuildClaim(deliveryId: first.DeliveryId, isSuccess: null);
        (await _store.TryAcquireDeliveryAttemptAsync(resume, TestContext.Current.CancellationToken)).ShouldBeTrue();

        // Between resume and the next Complete, the row keeps the prior attempt's audit fields
        // (ISO 27001: never zero out failure context — the next Complete will overwrite it).
        await using NotificationsDbContext db = _factory.CreateDbContext();
        NotificationDeliveryAttempt midFlight = await db.DeliveryAttempts
            .AsNoTracking()
            .SingleAsync(a => a.DeliveryId == first.DeliveryId, TestContext.Current.CancellationToken);

        midFlight.IsSuccess.ShouldBeNull();
        midFlight.ErrorMessage.ShouldBe("SMTP 421 service unavailable");
        midFlight.DurationMs.ShouldBe(750);
    }

    [Fact]
    public async Task Stuck_in_flight_row_older_than_timeout_can_be_reacquired()
    {
        DateTimeOffset t0 = new(2026, 5, 11, 12, 0, 0, TimeSpan.Zero);
        _now = t0;

        NotificationDeliveryAttempt stuck = BuildClaim(occurredAt: t0, isSuccess: null);
        (await _store.TryAcquireDeliveryAttemptAsync(stuck, TestContext.Current.CancellationToken)).ShouldBeTrue();

        // Simulate the original worker crashing without ever calling Complete: row remains IsSuccess = null.
        // Advance the clock past the in-flight timeout.
        _now = t0 + EfCoreNotificationDeliveryStore.InFlightClaimTimeout + TimeSpan.FromSeconds(1);

        NotificationDeliveryAttempt retry = BuildClaim(deliveryId: stuck.DeliveryId, isSuccess: null);
        (await _store.TryAcquireDeliveryAttemptAsync(retry, TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task Resume_of_long_stale_failed_row_bumps_OccurredAt_to_block_immediate_reacquire()
    {
        // Regression guard for the resume / TTL race: if OccurredAt is not refreshed when a
        // failed row is resumed, a concurrent worker arriving moments later evaluates the in-flight
        // TTL against the *original* OccurredAt and double-claims — re-introducing the duplicate
        // SMTP send that #947 set out to eliminate.
        DateTimeOffset t0 = new(2026, 5, 11, 12, 0, 0, TimeSpan.Zero);
        _now = t0;

        NotificationDeliveryAttempt initial = BuildClaim(occurredAt: t0, isSuccess: null);
        (await _store.TryAcquireDeliveryAttemptAsync(initial, TestContext.Current.CancellationToken)).ShouldBeTrue();
        await _store.CompleteDeliveryAttemptAsync(
            initial.DeliveryId,
            success: false,
            durationMilliseconds: 5,
            errorMessage: "channel down",
            TestContext.Current.CancellationToken);

        // Fast-forward well past the in-flight TTL — the failed row is now "long stale".
        _now = t0 + TimeSpan.FromMinutes(30);

        NotificationDeliveryAttempt retryA = BuildClaim(deliveryId: initial.DeliveryId, isSuccess: null);
        (await _store.TryAcquireDeliveryAttemptAsync(retryA, TestContext.Current.CancellationToken)).ShouldBeTrue();

        // Concurrent worker arrives 1 second later. With the regression, OccurredAt is still t0
        // → TTL check passes → double-claim. With the fix, OccurredAt was bumped to t0+30min
        // → TTL check fails → second acquire returns false.
        _now += TimeSpan.FromSeconds(1);

        NotificationDeliveryAttempt retryB = BuildClaim(deliveryId: initial.DeliveryId, isSuccess: null);
        (await _store.TryAcquireDeliveryAttemptAsync(retryB, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Stuck_in_flight_row_within_timeout_window_blocks_reacquire()
    {
        DateTimeOffset t0 = new(2026, 5, 11, 12, 0, 0, TimeSpan.Zero);
        _now = t0;

        NotificationDeliveryAttempt inFlight = BuildClaim(occurredAt: t0, isSuccess: null);
        (await _store.TryAcquireDeliveryAttemptAsync(inFlight, TestContext.Current.CancellationToken)).ShouldBeTrue();

        // Another worker tries the same delivery ~30 seconds later — within the timeout, so the
        // original claim still owns it and the second acquire must back off.
        _now = t0 + TimeSpan.FromSeconds(30);

        NotificationDeliveryAttempt concurrent = BuildClaim(deliveryId: inFlight.DeliveryId, isSuccess: null);
        (await _store.TryAcquireDeliveryAttemptAsync(concurrent, TestContext.Current.CancellationToken)).ShouldBeFalse();
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
