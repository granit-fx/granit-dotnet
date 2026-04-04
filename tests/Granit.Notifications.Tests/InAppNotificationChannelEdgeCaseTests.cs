using System.Text.Json;
using Granit.Domain;
using Granit.Guids;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Granit.Notifications.Internal;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

public sealed class InAppNotificationChannelEdgeCaseTests
{
    private readonly IUserNotificationWriter _userNotificationWriter = Substitute.For<IUserNotificationWriter>();
    private readonly IClock _clock;
    private readonly InAppNotificationChannel _channel;

    public InAppNotificationChannelEdgeCaseTests()
    {
        _clock = Substitute.For<IClock>();
        _clock.Now.Returns(_ => new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));
        _channel = new InAppNotificationChannel(_userNotificationWriter, new SimpleGuidGenerator(), _clock);
    }

    [Fact]
    public async Task SendAsync_maps_related_entity_fields()
    {
        // Arrange
        UserNotification? captured = null;
        _userNotificationWriter.InsertAsync(Arg.Any<UserNotification>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<UserNotification>();
                return Task.CompletedTask;
            });

        NotificationDeliveryContext context = new()
        {
            DeliveryId = Guid.NewGuid(),
            NotificationId = Guid.NewGuid(),
            NotificationTypeName = "test.notification",
            RecipientUserId = "user-1",
            Severity = NotificationSeverity.Info,
            Data = JsonSerializer.SerializeToElement(new { key = "value" }),
            RelatedEntity = new EntityReference("Invoice", "inv-42"),
            OccurredAt = DateTimeOffset.UtcNow,
        };

        // Act
        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        // Assert
        captured.ShouldNotBeNull();
        captured!.RelatedEntityType.ShouldBe("Invoice");
        captured.RelatedEntityId.ShouldBe("inv-42");
    }

    [Fact]
    public async Task SendAsync_null_related_entity_sets_null_fields()
    {
        // Arrange
        UserNotification? captured = null;
        _userNotificationWriter.InsertAsync(Arg.Any<UserNotification>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<UserNotification>();
                return Task.CompletedTask;
            });

        NotificationDeliveryContext context = new()
        {
            DeliveryId = Guid.NewGuid(),
            NotificationId = Guid.NewGuid(),
            NotificationTypeName = "test.notification",
            RecipientUserId = "user-1",
            Severity = NotificationSeverity.Info,
            Data = JsonSerializer.SerializeToElement(new { key = "value" }),
            RelatedEntity = null,
            OccurredAt = DateTimeOffset.UtcNow,
        };

        // Act
        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        // Assert
        captured.ShouldNotBeNull();
        captured!.RelatedEntityType.ShouldBeNull();
        captured.RelatedEntityId.ShouldBeNull();
    }

    [Fact]
    public async Task SendAsync_uses_clock_for_created_at()
    {
        // Arrange
        DateTimeOffset expectedTime = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        UserNotification? captured = null;
        _userNotificationWriter.InsertAsync(Arg.Any<UserNotification>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<UserNotification>();
                return Task.CompletedTask;
            });

        NotificationDeliveryContext context = new()
        {
            DeliveryId = Guid.NewGuid(),
            NotificationId = Guid.NewGuid(),
            NotificationTypeName = "test.notification",
            RecipientUserId = "user-1",
            Severity = NotificationSeverity.Info,
            Data = JsonSerializer.SerializeToElement(new { key = "value" }),
            OccurredAt = DateTimeOffset.UtcNow,
        };

        // Act
        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        // Assert
        captured.ShouldNotBeNull();
        captured!.CreatedAt.ShouldBe(expectedTime);
    }

    [Fact]
    public async Task SendAsync_sets_tenant_id_from_context()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        UserNotification? captured = null;
        _userNotificationWriter.InsertAsync(Arg.Any<UserNotification>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<UserNotification>();
                return Task.CompletedTask;
            });

        NotificationDeliveryContext context = new()
        {
            DeliveryId = Guid.NewGuid(),
            NotificationId = Guid.NewGuid(),
            NotificationTypeName = "test.notification",
            RecipientUserId = "user-1",
            Severity = NotificationSeverity.Info,
            Data = JsonSerializer.SerializeToElement(new { key = "value" }),
            TenantId = tenantId,
            OccurredAt = DateTimeOffset.UtcNow,
        };

        // Act
        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        // Assert
        captured.ShouldNotBeNull();
        captured!.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void Channel_name_is_InApp() =>
        _channel.Name.ShouldBe(NotificationChannels.InApp);
}
