// =============================================================================
// Tests - NotificationPreferenceResponse
// =============================================================================
// Vérifie que le record Response DTO expose les propriétés attendues.
// =============================================================================

using Granit.Notifications.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Endpoints.Tests;

public sealed class NotificationPreferenceResponseTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        DateTimeOffset modifiedAt = createdAt.AddMinutes(5);

        // Act
        NotificationPreferenceResponse response = new(id, "user-1", "NewMessage", "Email", true, createdAt, modifiedAt);

        // Assert
        response.Id.ShouldBe(id);
        response.UserId.ShouldBe("user-1");
        response.NotificationTypeName.ShouldBe("NewMessage");
        response.ChannelName.ShouldBe("Email");
        response.IsEnabled.ShouldBeTrue();
        response.CreatedAt.ShouldBe(createdAt);
        response.ModifiedAt.ShouldBe(modifiedAt);
    }

    [Fact]
    public void Record_Equality_SameValues_AreEqual()
    {
        var id = Guid.NewGuid();
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        new NotificationPreferenceResponse(id, "u", "T", "C", true, createdAt, null)
            .ShouldBe(new NotificationPreferenceResponse(id, "u", "T", "C", true, createdAt, null));
    }
}
