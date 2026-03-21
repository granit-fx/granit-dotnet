using System.Text.Json;
using Shouldly;
using Xunit;

namespace Granit.Notifications.WebPush.Tests;

public sealed class PushNotificationPayloadTests
{
    [Fact]
    public void Properties_SetCorrectly()
    {
        var notificationId = Guid.NewGuid();
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        JsonElement data = JsonSerializer.SerializeToElement(new { key = "value" });

        PushNotificationPayload payload = new()
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
        typeof(PushNotificationPayload).IsSealed.ShouldBeTrue();

    [Fact]
    public void IsRecord() =>
        typeof(PushNotificationPayload).GetMethod("<Clone>$").ShouldNotBeNull();
}
