using System.Text.Json;
using Granit.Core.Domain;
using Granit.Notifications.Domain;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests.Domain;

public sealed class UserNotificationTests
{
    [Fact]
    public void InheritsEntity() =>
        typeof(UserNotification).IsAssignableTo(typeof(Entity)).ShouldBeTrue();

    [Fact]
    public void ImplementsIMultiTenant() =>
        typeof(UserNotification).IsAssignableTo(typeof(IMultiTenant)).ShouldBeTrue();

    [Fact]
    public void IsSealed() =>
        typeof(UserNotification).IsSealed.ShouldBeTrue();

    [Fact]
    public void Create_SetsAllProperties()
    {
        var id = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        JsonElement data = JsonDocument.Parse("""{"key":"value"}""").RootElement;

        var notification = UserNotification.Create(
            id,
            notificationId,
            "order.created",
            NotificationSeverity.Warning,
            "user-42",
            data,
            now,
            tenantId,
            "Order",
            "ORD-001");

        notification.Id.ShouldBe(id);
        notification.NotificationId.ShouldBe(notificationId);
        notification.NotificationTypeName.ShouldBe("order.created");
        notification.Severity.ShouldBe(NotificationSeverity.Warning);
        notification.RecipientUserId.ShouldBe("user-42");
        notification.Data.GetProperty("key").GetString().ShouldBe("value");
        notification.State.ShouldBe(UserNotificationState.Unread);
        notification.CreatedAt.ShouldBe(now);
        notification.ReadAt.ShouldBeNull();
        notification.TenantId.ShouldBe(tenantId);
        notification.RelatedEntityType.ShouldBe("Order");
        notification.RelatedEntityId.ShouldBe("ORD-001");
    }

    [Fact]
    public void Create_DefaultState_IsUnread()
    {
        var notification = UserNotification.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "test.notification",
            NotificationSeverity.Info,
            "user-1",
            JsonSerializer.SerializeToElement(new { }),
            DateTimeOffset.UtcNow);

        notification.State.ShouldBe(UserNotificationState.Unread);
    }

    [Fact]
    public void MarkAsRead_SetsStateAndReadAt()
    {
        var notification = UserNotification.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "test.notification",
            NotificationSeverity.Info,
            "user-1",
            JsonSerializer.SerializeToElement(new { }),
            DateTimeOffset.UtcNow);

        DateTimeOffset readAt = DateTimeOffset.UtcNow.AddMinutes(5);
        notification.MarkAsRead(readAt);

        notification.State.ShouldBe(UserNotificationState.Read);
        notification.ReadAt.ShouldBe(readAt);
    }

    [Fact]
    public void MarkAsRead_AlreadyRead_DoesNotChangeReadAt()
    {
        var notification = UserNotification.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "test.notification",
            NotificationSeverity.Info,
            "user-1",
            JsonSerializer.SerializeToElement(new { }),
            DateTimeOffset.UtcNow);

        DateTimeOffset firstRead = DateTimeOffset.UtcNow.AddMinutes(5);
        notification.MarkAsRead(firstRead);

        DateTimeOffset secondRead = DateTimeOffset.UtcNow.AddMinutes(10);
        notification.MarkAsRead(secondRead);

        notification.ReadAt.ShouldBe(firstRead);
    }
}
