using System.Text.Json;
using Shouldly;
using Xunit;

namespace Granit.Notifications.WebPush.Tests;

public sealed class WebPushNotificationPayloadTests
{
    [Fact]
    public void Properties_SetCorrectly()
    {
        var notificationId = Guid.NewGuid();
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        JsonElement data = JsonSerializer.SerializeToElement(new { key = "value" });

        WebPushNotificationPayload payload = new()
        {
            NotificationId = notificationId,
            NotificationTypeName = "order.created",
            Severity = "Warning",
            Data = data,
            OccurredAt = occurredAt,
        };

        payload.NotificationId.ShouldBe(notificationId);
        payload.NotificationTypeName.ShouldBe("order.created");
        payload.Severity.ShouldBe("Warning");
        payload.OccurredAt.ShouldBe(occurredAt);
    }

    [Fact]
    public void IsSealed() =>
        typeof(WebPushNotificationPayload).IsSealed.ShouldBeTrue();

    [Fact]
    public void IsRecord() =>
        typeof(WebPushNotificationPayload).GetMethod("<Clone>$").ShouldNotBeNull();
}
