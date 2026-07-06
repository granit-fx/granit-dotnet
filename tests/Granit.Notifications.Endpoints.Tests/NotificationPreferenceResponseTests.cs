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
    public void Record_Equality_SameValues_AreEqual()
    {
        var id = Guid.NewGuid();
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        new NotificationPreferenceResponse(id, "u", "T", "C", true, createdAt, null)
            .ShouldBe(new NotificationPreferenceResponse(id, "u", "T", "C", true, createdAt, null));
    }
}
