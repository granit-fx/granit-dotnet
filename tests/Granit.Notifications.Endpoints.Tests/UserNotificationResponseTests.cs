// =============================================================================
// Tests - UserNotificationResponse
// =============================================================================
// Vérifie que le record Response DTO expose les propriétés attendues
// et que l'égalité structurelle fonctionne.
// =============================================================================

using Granit.Notifications.Domain;
using Granit.Notifications.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Endpoints.Tests;

public sealed class UserNotificationResponseTests
{
    [Fact]
    public void Record_Equality_SameValues_AreEqual()
    {
        var id = Guid.NewGuid();
        var nid = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        UserNotificationResponse a = new(id, nid, "T", NotificationSeverity.Info, "u", null, UserNotificationState.Unread, now, null, null, null);
        UserNotificationResponse b = new(id, nid, "T", NotificationSeverity.Info, "u", null, UserNotificationState.Unread, now, null, null, null);

        a.ShouldBe(b);
    }
}
