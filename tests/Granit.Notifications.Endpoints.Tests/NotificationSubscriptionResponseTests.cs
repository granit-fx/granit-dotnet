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
    public void Constructor_NullOptionalFields()
    {
        var id = Guid.NewGuid();
        NotificationSubscriptionResponse response = new(id, "user-1", "TopicSub", null, null, DateTimeOffset.UtcNow);

        response.EntityType.ShouldBeNull();
        response.EntityId.ShouldBeNull();
    }
}
