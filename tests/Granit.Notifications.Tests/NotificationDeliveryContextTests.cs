// =============================================================================
// Tests - NotificationDeliveryContext
// =============================================================================
// Verifies the record type passed to notification channels during delivery:
// required properties, optional properties, and record value equality.
// =============================================================================

using System.Text.Json;
using Granit.Domain;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

public sealed class NotificationDeliveryContextTests
{
    [Fact]
    public void RequiredProperties_AreSetCorrectly()
    {
        var notificationId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        JsonElement data = JsonSerializer.SerializeToElement(new { key = "value" });

        NotificationDeliveryContext context = new()
        {
            NotificationId = notificationId,
            DeliveryId = deliveryId,
            NotificationTypeName = "order.created",
            Severity = NotificationSeverity.Warning,
            RecipientUserId = "user-1",
            Data = data,
        };

        context.NotificationId.ShouldBe(notificationId);
        context.DeliveryId.ShouldBe(deliveryId);
        context.NotificationTypeName.ShouldBe("order.created");
        context.Severity.ShouldBe(NotificationSeverity.Warning);
        context.RecipientUserId.ShouldBe("user-1");
    }

    [Fact]
    public void OptionalProperties_DefaultToNull()
    {
        NotificationDeliveryContext context = new()
        {
            NotificationId = Guid.NewGuid(),
            DeliveryId = Guid.NewGuid(),
            NotificationTypeName = "test",
            Severity = NotificationSeverity.Info,
            RecipientUserId = "user-1",
            Data = JsonSerializer.SerializeToElement(new { }),
        };

        context.RelatedEntity.ShouldBeNull();
        context.TenantId.ShouldBeNull();
        context.Culture.ShouldBeNull();
    }

    [Fact]
    public void RelatedEntity_CanBeSet()
    {
        EntityReference entity = new("Invoice", "inv-42");

        NotificationDeliveryContext context = new()
        {
            NotificationId = Guid.NewGuid(),
            DeliveryId = Guid.NewGuid(),
            NotificationTypeName = "test",
            Severity = NotificationSeverity.Info,
            RecipientUserId = "user-1",
            Data = JsonSerializer.SerializeToElement(new { }),
            RelatedEntity = entity,
        };

        context.RelatedEntity.ShouldBe(entity);
    }

    [Fact]
    public void TenantId_CanBeSet()
    {
        var tenantId = Guid.NewGuid();

        NotificationDeliveryContext context = new()
        {
            NotificationId = Guid.NewGuid(),
            DeliveryId = Guid.NewGuid(),
            NotificationTypeName = "test",
            Severity = NotificationSeverity.Info,
            RecipientUserId = "user-1",
            Data = JsonSerializer.SerializeToElement(new { }),
            TenantId = tenantId,
        };

        context.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void Culture_CanBeSet()
    {
        NotificationDeliveryContext context = new()
        {
            NotificationId = Guid.NewGuid(),
            DeliveryId = Guid.NewGuid(),
            NotificationTypeName = "test",
            Severity = NotificationSeverity.Info,
            RecipientUserId = "user-1",
            Data = JsonSerializer.SerializeToElement(new { }),
            Culture = "fr-BE",
        };

        context.Culture.ShouldBe("fr-BE");
    }

    [Fact]
    public void OccurredAt_CanBeSet()
    {
        DateTimeOffset occurredAt = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

        NotificationDeliveryContext context = new()
        {
            NotificationId = Guid.NewGuid(),
            DeliveryId = Guid.NewGuid(),
            NotificationTypeName = "test",
            Severity = NotificationSeverity.Info,
            RecipientUserId = "user-1",
            Data = JsonSerializer.SerializeToElement(new { }),
            OccurredAt = occurredAt,
        };

        context.OccurredAt.ShouldBe(occurredAt);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var notificationId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        JsonElement data = JsonSerializer.SerializeToElement(new { key = "value" });
        DateTimeOffset occurredAt = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

        NotificationDeliveryContext a = new()
        {
            NotificationId = notificationId,
            DeliveryId = deliveryId,
            NotificationTypeName = "test",
            Severity = NotificationSeverity.Info,
            RecipientUserId = "user-1",
            Data = data,
            OccurredAt = occurredAt,
        };

        NotificationDeliveryContext b = new()
        {
            NotificationId = notificationId,
            DeliveryId = deliveryId,
            NotificationTypeName = "test",
            Severity = NotificationSeverity.Info,
            RecipientUserId = "user-1",
            Data = data,
            OccurredAt = occurredAt,
        };

        a.ShouldBe(b);
    }
}
