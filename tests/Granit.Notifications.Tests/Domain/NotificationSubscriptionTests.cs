using Granit.Domain;
using Granit.Notifications.Domain;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests.Domain;

public sealed class NotificationSubscriptionTests
{
    [Fact]
    public void InheritsCreationAuditedEntity() =>
        typeof(NotificationSubscription).IsAssignableTo(typeof(CreationAuditedEntity)).ShouldBeTrue();

    [Fact]
    public void ImplementsIMultiTenant() =>
        typeof(NotificationSubscription).IsAssignableTo(typeof(IMultiTenant)).ShouldBeTrue();

    [Fact]
    public void IsSealed() =>
        typeof(NotificationSubscription).IsSealed.ShouldBeTrue();

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        NotificationSubscription subscription = new();

        subscription.Id.ShouldBe(Guid.Empty);
        subscription.UserId.ShouldBe(string.Empty);
        subscription.NotificationTypeName.ShouldBe(string.Empty);
        subscription.TenantId.ShouldBeNull();
        subscription.EntityType.ShouldBeNull();
        subscription.EntityId.ShouldBeNull();
        subscription.CreatedAt.ShouldBe(default);
        subscription.CreatedBy.ShouldBe(string.Empty);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var tenantId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        NotificationSubscription subscription = new()
        {
            UserId = "user-42",
            NotificationTypeName = "order.shipped",
            TenantId = tenantId,
            EntityType = "Order",
            EntityId = "ORD-001",
            CreatedAt = now,
            CreatedBy = "admin",
        };

        subscription.UserId.ShouldBe("user-42");
        subscription.NotificationTypeName.ShouldBe("order.shipped");
        subscription.TenantId.ShouldBe(tenantId);
        subscription.EntityType.ShouldBe("Order");
        subscription.EntityId.ShouldBe("ORD-001");
        subscription.CreatedAt.ShouldBe(now);
        subscription.CreatedBy.ShouldBe("admin");
    }

    [Fact]
    public void TopicSubscription_HasNullEntityFields()
    {
        NotificationSubscription subscription = new()
        {
            UserId = "user-1",
            NotificationTypeName = "system.alert",
        };

        subscription.EntityType.ShouldBeNull();
        subscription.EntityId.ShouldBeNull();
    }
}
