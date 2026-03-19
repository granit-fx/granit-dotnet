using System.Text.Json;
using Granit.Core.Domain;
using Granit.Core.Events;
using Granit.Notifications.Domain;
using Granit.Notifications.Events;
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

    [Fact]
    public void RaiseCreatedEvent_ShouldEmitUserNotificationCreatedEvent()
    {
        var notificationId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var notification = UserNotification.Create(
            Guid.NewGuid(),
            notificationId,
            "order.created",
            NotificationSeverity.Warning,
            "user-42",
            JsonSerializer.SerializeToElement(new { }),
            DateTimeOffset.UtcNow,
            tenantId);

        notification.RaiseCreatedEvent();

        IDomainEvent domainEvent = notification.DomainEvents.ShouldHaveSingleItem();
        UserNotificationCreatedEvent created = domainEvent.ShouldBeOfType<UserNotificationCreatedEvent>();
        created.NotificationId.ShouldBe(notificationId);
        created.NotificationTypeName.ShouldBe("order.created");
        created.Severity.ShouldBe(NotificationSeverity.Warning);
        created.RecipientUserId.ShouldBe("user-42");
        created.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void MarkAsRead_ShouldEmitUserNotificationReadEvent()
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

        IDomainEvent domainEvent = notification.DomainEvents.ShouldHaveSingleItem();
        UserNotificationReadEvent read = domainEvent.ShouldBeOfType<UserNotificationReadEvent>();
        read.NotificationId.ShouldBe(notification.Id);
        read.RecipientUserId.ShouldBe("user-1");
        read.ReadAt.ShouldBe(readAt);
    }

    [Fact]
    public void MarkAsRead_AlreadyRead_DoesNotEmitSecondEvent()
    {
        var notification = UserNotification.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "test.notification",
            NotificationSeverity.Info,
            "user-1",
            JsonSerializer.SerializeToElement(new { }),
            DateTimeOffset.UtcNow);

        notification.MarkAsRead(DateTimeOffset.UtcNow.AddMinutes(5));
        notification.MarkAsRead(DateTimeOffset.UtcNow.AddMinutes(10));

        notification.DomainEvents.ShouldHaveSingleItem();
    }
}
