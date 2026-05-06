// =============================================================================
// Tests - NullNotificationDeliveryWriter
// =============================================================================
// Verifies the no-op delivery store used in development: acquire/complete are
// inert yet complete successfully (no persisted idempotency).
// =============================================================================

using Granit.Notifications.Domain;
using Granit.Notifications.Internal;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

public sealed class NullNotificationDeliveryWriterTests
{
    private readonly NullNotificationDeliveryWriter _store = new();

    [Fact]
    public async Task TryAcquireDeliveryAttemptAsync_CompletesSuccessfully()
    {
        NotificationDeliveryAttempt claim = BuildClaim();

        bool acquired = await _store.TryAcquireDeliveryAttemptAsync(claim, TestContext.Current.CancellationToken);

        acquired.ShouldBeTrue();
    }

    [Fact]
    public async Task CompleteDeliveryAttemptAsync_CompletesSuccessfully()
    {
        Func<Task> act = () => _store.CompleteDeliveryAttemptAsync(
            Guid.NewGuid(),
            success: true,
            durationMilliseconds: 10,
            errorMessage: null,
            TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task MultipleAcquireCalls_AllReturnTrue()
    {
        for (int i = 0; i < 10; i++)
        {
            bool acquired = await _store.TryAcquireDeliveryAttemptAsync(
                BuildClaim(), TestContext.Current.CancellationToken);

            acquired.ShouldBeTrue();
        }

        _store.ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteBeforeAsync_ReturnsZero()
    {
        int result = await _store.DeleteBeforeAsync(
            DateTimeOffset.UtcNow, batchSize: 100, TestContext.Current.CancellationToken);

        result.ShouldBe(0);
    }

    [Fact]
    public async Task DeleteBeforeAsync_WithAnyCutoff_ReturnsZero()
    {
        int result = await _store.DeleteBeforeAsync(
            DateTimeOffset.MinValue, batchSize: 1, TestContext.Current.CancellationToken);

        result.ShouldBe(0);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static NotificationDeliveryAttempt BuildClaim() => new()
    {
        Id = Guid.NewGuid(),
        DeliveryId = Guid.NewGuid(),
        NotificationId = Guid.NewGuid(),
        NotificationTypeName = "test.notification",
        ChannelName = NotificationChannels.InApp,
        RecipientUserId = "user-1",
        OccurredAt = DateTimeOffset.UtcNow,
        DurationMs = 0,
        IsSuccess = null,
        ErrorMessage = null,
    };
}
