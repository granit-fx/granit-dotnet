// =============================================================================
// Tests - NotificationsOptions
// =============================================================================
// Verifies default values and configuration for the notification engine options.
// =============================================================================

using Granit.Notifications.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

public sealed class NotificationsOptionsTests
{
    [Fact]
    public void SectionName_IsNotifications() =>
        NotificationsOptions.SectionName.ShouldBe("Notifications");

    [Fact]
    public void MaxParallelDeliveries_DefaultIsEight()
    {
        NotificationsOptions options = new();

        options.MaxParallelDeliveries.ShouldBe(8);
    }
}
