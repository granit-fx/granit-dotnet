// =============================================================================
// Tests - NotificationChannels
// =============================================================================
// Verifies well-known channel name constants are correctly defined.
// Guards against accidental renames that would break channel routing.
// =============================================================================

using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

public sealed class NotificationChannelsTests
{
    [Fact]
    public void InApp_HasExpectedValue() =>
        NotificationChannels.InApp.ShouldBe("InApp");

    [Fact]
    public void SignalR_HasExpectedValue() =>
        NotificationChannels.SignalR.ShouldBe("SignalR");

    [Fact]
    public void Email_HasExpectedValue() =>
        NotificationChannels.Email.ShouldBe("Email");

    [Fact]
    public void Sms_HasExpectedValue() =>
        NotificationChannels.Sms.ShouldBe("Sms");

    [Fact]
    public void WhatsApp_HasExpectedValue() =>
        NotificationChannels.WhatsApp.ShouldBe("WhatsApp");

    [Fact]
    public void WebPush_HasExpectedValue() =>
        NotificationChannels.WebPush.ShouldBe("WebPush");
}
