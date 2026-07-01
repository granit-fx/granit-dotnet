// =============================================================================
// Tests - DeliverNotificationCommand
// =============================================================================
// Verifies the Wolverine command record produced by fan-out: required
// properties, optional property defaults, and full property initialization.
// =============================================================================

using System.Text.Json;
using Granit.Domain;
using Granit.Notifications.Messages;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

public sealed class DeliverNotificationCommandTests
{
    [Fact]
    public void RequiredProperties_AreSetCorrectly()
    {
        var deliveryId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        DateTimeOffset occurredAt = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        JsonElement data = JsonSerializer.SerializeToElement(new { key = "value" });

        DeliverNotificationCommand command = new()
        {
            DeliveryId = deliveryId,
            NotificationId = notificationId,
            NotificationTypeName = "order.shipped",
            Severity = NotificationSeverity.Success,
            RecipientUserId = "user-42",
            ChannelName = NotificationChannels.Email,
            Data = data,
            OccurredAt = occurredAt,
        };

        command.DeliveryId.ShouldBe(deliveryId);
        command.NotificationId.ShouldBe(notificationId);
        command.NotificationTypeName.ShouldBe("order.shipped");
        command.Severity.ShouldBe(NotificationSeverity.Success);
        command.RecipientUserId.ShouldBe("user-42");
        command.ChannelName.ShouldBe(NotificationChannels.Email);
        command.OccurredAt.ShouldBe(occurredAt);
    }

    [Fact]
    public void OptionalProperties_DefaultToNull()
    {
        DeliverNotificationCommand command = new()
        {
            DeliveryId = Guid.NewGuid(),
            NotificationId = Guid.NewGuid(),
            NotificationTypeName = "test",
            Severity = NotificationSeverity.Info,
            RecipientUserId = "user-1",
            ChannelName = NotificationChannels.InApp,
            Data = JsonSerializer.SerializeToElement(new { }),
            OccurredAt = DateTimeOffset.UtcNow,
        };

        command.RelatedEntity.ShouldBeNull();
        command.TenantId.ShouldBeNull();
        command.Culture.ShouldBeNull();
    }

    [Fact]
    public void AllProperties_CanBeSetViaInitializers()
    {
        var deliveryId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        DateTimeOffset occurredAt = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        EntityReference entity = new("Document", "doc-99");
        JsonElement data = JsonSerializer.SerializeToElement(new { body = "hello" });

        DeliverNotificationCommand command = new()
        {
            DeliveryId = deliveryId,
            NotificationId = notificationId,
            NotificationTypeName = "doc.updated",
            Severity = NotificationSeverity.Warning,
            RecipientUserId = "user-admin",
            ChannelName = NotificationChannels.WebPush,
            Data = data,
            RelatedEntity = entity,
            TenantId = tenantId,
            OccurredAt = occurredAt,
            Culture = "de-DE",
        };

        command.RelatedEntity.ShouldBe(entity);
        command.TenantId.ShouldBe(tenantId);
        command.Culture.ShouldBe("de-DE");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var deliveryId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        DateTimeOffset occurredAt = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        JsonElement data = JsonSerializer.SerializeToElement(new { key = "value" });

        DeliverNotificationCommand a = new()
        {
            DeliveryId = deliveryId,
            NotificationId = notificationId,
            NotificationTypeName = "test",
            Severity = NotificationSeverity.Info,
            RecipientUserId = "user-1",
            ChannelName = NotificationChannels.InApp,
            Data = data,
            OccurredAt = occurredAt,
        };

        DeliverNotificationCommand b = new()
        {
            DeliveryId = deliveryId,
            NotificationId = notificationId,
            NotificationTypeName = "test",
            Severity = NotificationSeverity.Info,
            RecipientUserId = "user-1",
            ChannelName = NotificationChannels.InApp,
            Data = data,
            OccurredAt = occurredAt,
        };

        a.ShouldBe(b);
    }
}
