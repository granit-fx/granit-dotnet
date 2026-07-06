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
        attempt.IsSuccess.ShouldBeNull();
    }

    [Fact]
    public void FailedAttempt_HasErrorMessage()
    {
        NotificationDeliveryAttempt attempt = new()
        {
            IsSuccess = false,
            ErrorMessage = "SMTP timeout",
        };

        attempt.IsSuccess.ShouldBe(false);
        attempt.ErrorMessage.ShouldBe("SMTP timeout");
    }
}
