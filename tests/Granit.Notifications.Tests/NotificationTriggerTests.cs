// =============================================================================
// Tests - NotificationTrigger
// =============================================================================
// Verifies the Wolverine message record: required properties, default values,
// optional property assignments, and auto-generated NotificationId.
// =============================================================================

using System.Text.Json;
using Granit.Domain;
using Granit.Notifications.Messages;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

public sealed class NotificationTriggerTests
{
    [Fact]
    public void NotificationId_DefaultsToEmpty_PublishersAssignIt()
    {
        // UUID v7 assignment moved to the publishers (IGuidGenerator) — the record no
        // longer self-assigns a random v4 (GRSEC002).
        NotificationTrigger trigger = new()
        {
            NotificationTypeName = "test.notification",
            Severity = NotificationSeverity.Info,
            Data = JsonSerializer.SerializeToElement(new { }),
            OccurredAt = DateTimeOffset.UtcNow,
        };

        trigger.NotificationId.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void NotificationId_IsInitSettable_ByPublishers()
    {
        var id = Guid.NewGuid();

        NotificationTrigger trigger = new()
        {
            NotificationId = id,
            NotificationTypeName = "test",
            Severity = NotificationSeverity.Info,
            Data = JsonSerializer.SerializeToElement(new { }),
            OccurredAt = DateTimeOffset.UtcNow,
        };

        trigger.NotificationId.ShouldBe(id);
    }

    [Fact]
    public void RecipientUserIds_DefaultsToEmptyList()
    {
        NotificationTrigger trigger = new()
        {
            NotificationTypeName = "test",
            Severity = NotificationSeverity.Info,
            Data = JsonSerializer.SerializeToElement(new { }),
            OccurredAt = DateTimeOffset.UtcNow,
        };

        trigger.RecipientUserIds.ShouldBeEmpty();
    }

    [Fact]
    public void RelatedEntity_DefaultsToNull()
    {
        NotificationTrigger trigger = new()
        {
            NotificationTypeName = "test",
            Severity = NotificationSeverity.Info,
            Data = JsonSerializer.SerializeToElement(new { }),
            OccurredAt = DateTimeOffset.UtcNow,
        };

        trigger.RelatedEntity.ShouldBeNull();
    }

    [Fact]
    public void TenantId_DefaultsToNull()
    {
        NotificationTrigger trigger = new()
        {
            NotificationTypeName = "test",
            Severity = NotificationSeverity.Info,
            Data = JsonSerializer.SerializeToElement(new { }),
            OccurredAt = DateTimeOffset.UtcNow,
        };

        trigger.TenantId.ShouldBeNull();
    }

    [Fact]
    public void Culture_DefaultsToNull()
    {
        NotificationTrigger trigger = new()
        {
            NotificationTypeName = "test",
            Severity = NotificationSeverity.Info,
            Data = JsonSerializer.SerializeToElement(new { }),
            OccurredAt = DateTimeOffset.UtcNow,
        };

        trigger.Culture.ShouldBeNull();
    }

    [Fact]
    public void AllProperties_CanBeSetViaInitializers()
    {
        var notificationId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        DateTimeOffset occurredAt = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        EntityReference entity = new("Invoice", "inv-42");
        JsonElement data = JsonSerializer.SerializeToElement(new { orderId = "123" });

        NotificationTrigger trigger = new()
        {
            NotificationId = notificationId,
            NotificationTypeName = "order.created",
            Severity = NotificationSeverity.Success,
            Data = data,
            RecipientUserIds = ["user-1", "user-2"],
            RelatedEntity = entity,
            TenantId = tenantId,
            OccurredAt = occurredAt,
            Culture = "fr-BE",
        };

        trigger.NotificationId.ShouldBe(notificationId);
        trigger.NotificationTypeName.ShouldBe("order.created");
        trigger.Severity.ShouldBe(NotificationSeverity.Success);
        trigger.RecipientUserIds.Count.ShouldBe(2);
        trigger.RelatedEntity.ShouldBe(entity);
        trigger.TenantId.ShouldBe(tenantId);
        trigger.OccurredAt.ShouldBe(occurredAt);
        trigger.Culture.ShouldBe("fr-BE");
    }
}
