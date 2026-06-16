// =============================================================================
// Tests - NotificationSubscriptionResponse
// =============================================================================
// Vérifie que le record Response DTO expose les propriétés attendues.
// =============================================================================

using Granit.Notifications.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Endpoints.Tests;

public sealed class NotificationSubscriptionResponseTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;

        // Act
        NotificationSubscriptionResponse response = new(id, "user-1", "NewMessage", "Acme.Patients", "p-42", createdAt);

        // Assert
        response.Id.ShouldBe(id);
        response.UserId.ShouldBe("user-1");
        response.NotificationTypeName.ShouldBe("NewMessage");
        response.EntityType.ShouldBe("Acme.Patients");
        response.EntityId.ShouldBe("p-42");
        response.CreatedAt.ShouldBe(createdAt);
    }

    [Fact]
    public void Constructor_NullOptionalFields()
    {
        var id = Guid.NewGuid();
        NotificationSubscriptionResponse response = new(id, "user-1", "TopicSub", null, null, DateTimeOffset.UtcNow);

        response.EntityType.ShouldBeNull();
        response.EntityId.ShouldBeNull();
    }
}
