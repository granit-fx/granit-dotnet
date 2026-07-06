// =============================================================================
// Tests - TrackedPropertyConfig
// =============================================================================
// Verifies the configuration record for auto-tracked entity properties:
// required notification type name, default severity, and custom severity.
// =============================================================================

using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

public sealed class TrackedPropertyConfigTests
{
    [Fact]
    public void Severity_DefaultIsInfo()
    {
        TrackedPropertyConfig config = new()
        {
            NotificationTypeName = "test.changed",
        };

        config.Severity.ShouldBe(NotificationSeverity.Info);
    }

    [Fact]
    public void Severity_CanBeSetToWarning()
    {
        TrackedPropertyConfig config = new()
        {
            NotificationTypeName = "patient.status.changed",
            Severity = NotificationSeverity.Warning,
        };

        config.Severity.ShouldBe(NotificationSeverity.Warning);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        TrackedPropertyConfig a = new()
        {
            NotificationTypeName = "test.changed",
            Severity = NotificationSeverity.Error,
        };

        TrackedPropertyConfig b = new()
        {
            NotificationTypeName = "test.changed",
            Severity = NotificationSeverity.Error,
        };

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentSeverity_AreNotEqual()
    {
        TrackedPropertyConfig a = new()
        {
            NotificationTypeName = "test.changed",
            Severity = NotificationSeverity.Info,
        };

        TrackedPropertyConfig b = new()
        {
            NotificationTypeName = "test.changed",
            Severity = NotificationSeverity.Fatal,
        };

        a.ShouldNotBe(b);
    }
}
