using Granit.Domain;
using Granit.Notifications.Domain;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests.Domain;

public sealed class NotificationDeliveryAttemptTests
{
    [Fact]
    public void InheritsEntity() =>
        typeof(NotificationDeliveryAttempt).IsAssignableTo(typeof(Entity)).ShouldBeTrue();

    [Fact]
    public void IsSealed() =>
        typeof(NotificationDeliveryAttempt).IsSealed.ShouldBeTrue();

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        NotificationDeliveryAttempt attempt = new();

        attempt.Id.ShouldBe(Guid.Empty);
        attempt.DeliveryId.ShouldBe(Guid.Empty);
        attempt.NotificationId.ShouldBe(Guid.Empty);
        attempt.NotificationTypeName.ShouldBe(string.Empty);
        attempt.ChannelName.ShouldBe(string.Empty);
        attempt.RecipientUserId.ShouldBe(string.Empty);
        attempt.TenantId.ShouldBeNull();
        attempt.OccurredAt.ShouldBe(default);
        attempt.DurationMs.ShouldBe(0);
        attempt.ErrorMessage.ShouldBeNull();
        attempt.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var deliveryId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        NotificationDeliveryAttempt attempt = new()
        {
            DeliveryId = deliveryId,
            NotificationId = notificationId,
            NotificationTypeName = "order.created",
            ChannelName = "Email",
            RecipientUserId = "user-42",
            TenantId = tenantId,
            OccurredAt = now,
            DurationMs = 150,
            ErrorMessage = null,
            IsSuccess = true,
        };

        attempt.DeliveryId.ShouldBe(deliveryId);
        attempt.NotificationId.ShouldBe(notificationId);
        attempt.NotificationTypeName.ShouldBe("order.created");
        attempt.ChannelName.ShouldBe("Email");
        attempt.RecipientUserId.ShouldBe("user-42");
        attempt.TenantId.ShouldBe(tenantId);
        attempt.OccurredAt.ShouldBe(now);
        attempt.DurationMs.ShouldBe(150);
        attempt.ErrorMessage.ShouldBeNull();
        attempt.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void FailedAttempt_HasErrorMessage()
    {
        NotificationDeliveryAttempt attempt = new()
        {
            IsSuccess = false,
            ErrorMessage = "SMTP timeout",
        };

        attempt.IsSuccess.ShouldBeFalse();
        attempt.ErrorMessage.ShouldBe("SMTP timeout");
    }
}
